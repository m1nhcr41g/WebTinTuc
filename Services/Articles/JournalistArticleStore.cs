using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Articles;
using System.Text.RegularExpressions;

namespace WebTinTuc.Services.Articles;

public class JournalistArticleStore
{
    private readonly string _connectionString;

    public JournalistArticleStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found.");
    }

    public async Task EnsureSchemaAsync()
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS journalist_articles (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                author_account_id INTEGER NOT NULL,
                author_name TEXT NOT NULL,
                title TEXT NOT NULL,
                slug TEXT NOT NULL DEFAULT '',
                category_id TEXT NULL,
                summary TEXT NOT NULL DEFAULT '',
                thumbnail_url TEXT NOT NULL DEFAULT 'WebTinTuc\wwwroot\images\default-thumb.jpg',
                content TEXT NOT NULL,
                view_count INTEGER NOT NULL DEFAULT 0,
                created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at_utc TEXT NULL,
                FOREIGN KEY(author_account_id) REFERENCES auth_accounts(id)
            );
            CREATE INDEX IF NOT EXISTS idx_journalist_articles_author_id
                ON journalist_articles(author_account_id);
            CREATE INDEX IF NOT EXISTS idx_journalist_articles_created_at
                ON journalist_articles(created_at_utc DESC);

            CREATE TABLE IF NOT EXISTS journalist_article_tags (
                article_id INTEGER NOT NULL,
                tag_id TEXT NOT NULL,
                PRIMARY KEY (article_id, tag_id),
                FOREIGN KEY(article_id) REFERENCES journalist_articles(id) ON DELETE CASCADE,
                FOREIGN KEY(tag_id) REFERENCES tags(id)
            );
            CREATE INDEX IF NOT EXISTS idx_journalist_article_tags_article_id
                ON journalist_article_tags(article_id);
            CREATE INDEX IF NOT EXISTS idx_journalist_article_tags_tag_id
                ON journalist_article_tags(tag_id);";

        await command.ExecuteNonQueryAsync();

        await EnsureViewCountColumnAsync(connection);
        await EnsureSlugColumnAsync(connection);
        await EnsureCategoryIdColumnAsync(connection);
        await EnsureThumbnailUrlColumnAsync(connection);
        await EnsureJournalistArticleTagsTagIdTextAsync(connection);
        await BackfillMissingSlugsAsync(connection);
        await BackfillMissingThumbnailsAsync(connection);

        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = @"
            CREATE INDEX IF NOT EXISTS idx_journalist_articles_view_count
                ON journalist_articles(view_count DESC);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_journalist_articles_slug
                ON journalist_articles(slug);";
        await indexCommand.ExecuteNonQueryAsync();
    }

    public async Task<List<JournalistArticle>> GetByAuthorAsync(long authorAccountId)
    {
        var result = new List<JournalistArticle>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            WHERE a.author_account_id = $authorAccountId
            ORDER BY datetime(a.created_at_utc) DESC;";
        command.Parameters.AddWithValue("$authorAccountId", authorAccountId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<JournalistArticle?> GetByIdAsync(long id, long authorAccountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            WHERE a.id = $id AND a.author_account_id = $authorAccountId
            LIMIT 1;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$authorAccountId", authorAccountId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapArticle(reader);
    }

    public async Task<List<JournalistArticle>> GetHotArticlesAsync(int limit)
    {
        var result = new List<JournalistArticle>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
                        WHERE a.view_count > 0
            ORDER BY a.view_count DESC, datetime(a.created_at_utc) DESC
            LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<List<JournalistArticle>> GetLatestArticlesAsync(int limit)
    {
        var result = new List<JournalistArticle>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            ORDER BY datetime(a.created_at_utc) DESC
            LIMIT $limit;";
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<List<JournalistArticle>> GetArticlesByTagAsync(string tagName, int limit)
    {
        if (string.IsNullOrWhiteSpace(tagName))
        {
            return new List<JournalistArticle>();
        }

        var result = new List<JournalistArticle>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        if (displayColumn is null)
        {
            return result;
        }

        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT DISTINCT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                   a.view_count,
                   {tagsSelect} AS tags_text,
                   a.category_id,
                   {BuildCategoryNameSelect("a")} AS category_name,
                   {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            JOIN journalist_article_tags jat ON jat.article_id = a.id
            JOIN tags t ON t.id = jat.tag_id
            WHERE lower(trim(t.{displayColumn})) = lower(trim($tagName))
            ORDER BY datetime(a.created_at_utc) DESC
            LIMIT $limit;";
        command.Parameters.AddWithValue("$tagName", tagName.Trim());
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<List<JournalistArticle>> GetArticlesByCategorySlugAsync(string categorySlug, int limit)
    {
        if (string.IsNullOrWhiteSpace(categorySlug))
        {
            return new List<JournalistArticle>();
        }

        var result = new List<JournalistArticle>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                   a.view_count,
                   {tagsSelect} AS tags_text,
                   a.category_id,
                   {BuildCategoryNameSelect("a")} AS category_name,
                   {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            WHERE a.category_id = (
                SELECT id
                FROM categories
                WHERE lower(slug) = lower($categorySlug)
                LIMIT 1
            )
            ORDER BY datetime(a.created_at_utc) DESC
            LIMIT $limit;";
        command.Parameters.AddWithValue("$categorySlug", categorySlug.Trim());
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<List<JournalistArticle>> GetRecommendedArticlesAsync(IReadOnlyCollection<string> keywords, int limit)
    {
        var normalizedKeywords = keywords
            .Where(keyword => !string.IsNullOrWhiteSpace(keyword))
            .Select(keyword => keyword.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedKeywords.Count == 0)
        {
            return new List<JournalistArticle>();
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        var tagScoreParts = new List<string>();
        var contentScoreParts = new List<string>();
        var whereParts = new List<string>();

        await using var command = connection.CreateCommand();
        for (var index = 0; index < normalizedKeywords.Count; index++)
        {
            var parameterName = $"$keyword{index}";
            var likeValue = $"%{normalizedKeywords[index]}%";

            command.Parameters.AddWithValue(parameterName, likeValue);

            var tagMatchExpression = $"({tagsSelect} LIKE {parameterName} COLLATE NOCASE)";
            var contentMatchExpression = $"(a.title LIKE {parameterName} COLLATE NOCASE OR a.summary LIKE {parameterName} COLLATE NOCASE OR a.content LIKE {parameterName} COLLATE NOCASE)";

            tagScoreParts.Add($"CASE WHEN {tagMatchExpression} THEN 1 ELSE 0 END");
            contentScoreParts.Add($"CASE WHEN {contentMatchExpression} THEN 1 ELSE 0 END");
            whereParts.Add($"({tagMatchExpression} OR {contentMatchExpression})");
        }

        command.Parameters.AddWithValue("$limit", limit);

        command.CommandText = $@"
                                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                                     a.view_count,
                   {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug,
                   ({string.Join(" + ", tagScoreParts)}) AS tag_score,
                   ({string.Join(" + ", contentScoreParts)}) AS content_score
            FROM journalist_articles a
            WHERE {string.Join(" OR ", whereParts)}
            ORDER BY tag_score DESC, content_score DESC, datetime(a.created_at_utc) DESC
            LIMIT $limit;";

        var result = new List<JournalistArticle>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(MapArticle(reader));
        }

        return result;
    }

    public async Task<List<NewsTagSectionViewModel>> GetTagSectionsAsync(int sectionCount, int articlesPerSection)
    {
        var result = new List<NewsTagSectionViewModel>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        if (displayColumn is null)
        {
            return result;
        }

        await using var tagsCommand = connection.CreateCommand();
        tagsCommand.CommandText = $@"
            SELECT t.id, t.{displayColumn} AS display_name, COUNT(jat.article_id) AS article_count
            FROM journalist_article_tags jat
            JOIN tags t ON t.id = jat.tag_id
            GROUP BY t.id, t.{displayColumn}
            ORDER BY article_count DESC, display_name
            LIMIT $sectionCount;";
        tagsCommand.Parameters.AddWithValue("$sectionCount", sectionCount);

        var topTags = new List<(long Id, string Name)>();
        await using (var tagReader = await tagsCommand.ExecuteReaderAsync())
        {
            while (await tagReader.ReadAsync())
            {
                if (!tagReader.IsDBNull(1))
                {
                    topTags.Add((tagReader.GetInt64(0), tagReader.GetString(1)));
                }
            }
        }

        var tagsSelect = BuildTagsSelect(displayColumn, "a");
        foreach (var (tagId, tagName) in topTags)
        {
            await using var articlesCommand = connection.CreateCommand();
            articlesCommand.CommandText = $@"
                   SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                       a.view_count,
                      {tagsSelect} AS tags_text,
                      a.category_id,
                      {BuildCategoryNameSelect("a")} AS category_name,
                      {BuildCategorySlugSelect("a")} AS category_slug
                FROM journalist_articles a
                JOIN journalist_article_tags jat ON jat.article_id = a.id
                WHERE jat.tag_id = $tagId
                ORDER BY datetime(a.created_at_utc) DESC
                LIMIT $limit;";
            articlesCommand.Parameters.AddWithValue("$tagId", tagId);
            articlesCommand.Parameters.AddWithValue("$limit", articlesPerSection);

            var articles = new List<JournalistArticle>();
            await using var articleReader = await articlesCommand.ExecuteReaderAsync();
            while (await articleReader.ReadAsync())
            {
                articles.Add(MapArticle(articleReader));
            }

            if (articles.Count > 0)
            {
                result.Add(new NewsTagSectionViewModel
                {
                    TagName = tagName,
                    Articles = articles
                });
            }
        }

        return result;
    }

    public async Task<JournalistArticle?> GetPublicBySlugAsync(string slug)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            WHERE lower(a.slug) = lower($slug)
            LIMIT 1;";
        command.Parameters.AddWithValue("$slug", slug.Trim());

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapArticle(reader);
    }

    public async Task<JournalistArticle?> GetPublicByIdAsync(long articleId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
                 SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                     a.view_count,
                     {tagsSelect} AS tags_text,
                     a.category_id,
                     {BuildCategoryNameSelect("a")} AS category_name,
                     {BuildCategorySlugSelect("a")} AS category_slug
            FROM journalist_articles a
            WHERE a.id = $articleId
            LIMIT 1;";
        command.Parameters.AddWithValue("$articleId", articleId);

        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return MapArticle(reader);
    }

    public async Task IncreaseViewCountAsync(long articleId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE journalist_articles
            SET view_count = COALESCE(view_count, 0) + 1
            WHERE id = $articleId;";
        command.Parameters.AddWithValue("$articleId", articleId);
        await command.ExecuteNonQueryAsync();
    }

    public async Task<long> CreateAsync(long authorAccountId, string authorName, ArticleFormViewModel model)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        var slug = await GenerateUniqueSlugAsync(connection, transaction, model.Title, null);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO journalist_articles (author_account_id, author_name, title, slug, category_id, summary, thumbnail_url, content)
            VALUES ($authorAccountId, $authorName, $title, $slug, $categoryId, $summary, $thumbnailUrl, $content);
            SELECT last_insert_rowid();";
        command.Parameters.AddWithValue("$authorAccountId", authorAccountId);
        command.Parameters.AddWithValue("$authorName", authorName);
        command.Parameters.AddWithValue("$title", model.Title.Trim());
        command.Parameters.AddWithValue("$slug", slug);
        command.Parameters.AddWithValue("$categoryId", string.IsNullOrWhiteSpace(model.CategoryId) ? DBNull.Value : model.CategoryId.Trim());
        command.Parameters.AddWithValue("$summary", (model.Summary ?? string.Empty).Trim());
        command.Parameters.AddWithValue("$thumbnailUrl", model.ThumbnailUrl.Trim());
        command.Parameters.AddWithValue("$content", model.Content.Trim());

        var result = await command.ExecuteScalarAsync();
        var articleId = Convert.ToInt64(result);

        await ReplaceArticleTagsAsync(connection, transaction, articleId, model.SelectedTagIds, model.NewTags ?? string.Empty);

        await transaction.CommitAsync();
        return articleId;
    }

    public async Task<bool> UpdateAsync(long id, long authorAccountId, ArticleFormViewModel model)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        var slug = await GenerateUniqueSlugAsync(connection, transaction, model.Title, id);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            UPDATE journalist_articles
            SET title = $title,
                slug = $slug,
                category_id = $categoryId,
                summary = $summary,
                thumbnail_url = $thumbnailUrl,
                content = $content,
                updated_at_utc = CURRENT_TIMESTAMP
            WHERE id = $id AND author_account_id = $authorAccountId;";

        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$authorAccountId", authorAccountId);
        command.Parameters.AddWithValue("$title", model.Title.Trim());
        command.Parameters.AddWithValue("$slug", slug);
        command.Parameters.AddWithValue("$categoryId", string.IsNullOrWhiteSpace(model.CategoryId) ? DBNull.Value : model.CategoryId.Trim());
        command.Parameters.AddWithValue("$summary", (model.Summary ?? string.Empty).Trim());
        command.Parameters.AddWithValue("$thumbnailUrl", model.ThumbnailUrl.Trim());
        command.Parameters.AddWithValue("$content", model.Content.Trim());

        var affectedRows = await command.ExecuteNonQueryAsync();
        if (affectedRows <= 0)
        {
            await transaction.RollbackAsync();
            return false;
        }

        await ReplaceArticleTagsAsync(connection, transaction, id, model.SelectedTagIds, model.NewTags ?? string.Empty);

        await transaction.CommitAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(long id, long authorAccountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            DELETE FROM journalist_articles
            WHERE id = $id AND author_account_id = $authorAccountId;";
        command.Parameters.AddWithValue("$id", id);
        command.Parameters.AddWithValue("$authorAccountId", authorAccountId);

        var affectedRows = await command.ExecuteNonQueryAsync();
        return affectedRows > 0;
    }

    public async Task<List<TagOption>> GetAllTagsAsync()
    {
        var result = new List<TagOption>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        if (displayColumn is null)
        {
            return result;
        }

        await using var command = connection.CreateCommand();
        command.CommandText = $@"
            SELECT id, {displayColumn} AS display_name
            FROM tags
            ORDER BY display_name;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(1))
            {
                continue;
            }

            result.Add(new TagOption
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1)
            });
        }

        return result;
    }

    public async Task<List<CategoryOption>> GetAllCategoriesAsync()
    {
        var result = new List<CategoryOption>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, name, slug
            FROM categories
            ORDER BY name;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new CategoryOption
            {
                Id = reader.GetString(0),
                Name = reader.GetString(1),
                Slug = reader.GetString(2)
            });
        }

        return result;
    }

    public async Task<List<NewsCategorySectionViewModel>> GetCategorySectionsAsync(int articlesPerCategory, string? categorySlug = null)
    {
        var result = new List<NewsCategorySectionViewModel>();

        var categories = await GetAllCategoriesAsync();
        if (!string.IsNullOrWhiteSpace(categorySlug))
        {
            categories = categories
                .Where(category => category.Slug.Equals(categorySlug.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        if (categories.Count == 0)
        {
            return result;
        }

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var displayColumn = await GetTagDisplayColumnAsync(connection);
        var tagsSelect = BuildTagsSelect(displayColumn, "a");

        foreach (var category in categories)
        {
            await using var totalViewsCommand = connection.CreateCommand();
            totalViewsCommand.CommandText = @"
                SELECT COALESCE(SUM(view_count), 0)
                FROM journalist_articles
                WHERE category_id = $categoryId;";
            totalViewsCommand.Parameters.AddWithValue("$categoryId", category.Id);
            var totalViewCount = Convert.ToInt64(await totalViewsCommand.ExecuteScalarAsync());

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
                  SELECT a.id, a.author_account_id, a.author_name, a.title, a.slug, a.summary, a.thumbnail_url, a.content, a.created_at_utc, a.updated_at_utc,
                      a.view_count,
                       {tagsSelect} AS tags_text,
                       a.category_id,
                       {BuildCategoryNameSelect("a")} AS category_name,
                       {BuildCategorySlugSelect("a")} AS category_slug
                FROM journalist_articles a
                WHERE a.category_id = $categoryId
                ORDER BY datetime(a.created_at_utc) DESC
                LIMIT $limit;";
            command.Parameters.AddWithValue("$categoryId", category.Id);
            command.Parameters.AddWithValue("$limit", articlesPerCategory);

            var articles = new List<JournalistArticle>();
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                articles.Add(MapArticle(reader));
            }

            if (articles.Count == 0)
            {
                continue;
            }

            result.Add(new NewsCategorySectionViewModel
            {
                CategoryId = category.Id,
                CategoryName = category.Name,
                CategorySlug = category.Slug,
                TotalViewCount = totalViewCount,
                Articles = articles
            });
        }

        return result;
    }

    public async Task<List<string>> GetTagIdsByArticleAsync(long articleId)
    {
        var tagIds = new List<string>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT tag_id
            FROM journalist_article_tags
            WHERE article_id = $articleId
            ORDER BY tag_id;";
        command.Parameters.AddWithValue("$articleId", articleId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            tagIds.Add(reader.GetString(0));
        }

        return tagIds;
    }

    private static JournalistArticle MapArticle(SqliteDataReader reader)
    {
        return new JournalistArticle
        {
            Id = reader.GetInt64(0),
            AuthorAccountId = reader.GetInt64(1),
            AuthorName = reader.GetString(2),
            Title = reader.GetString(3),
            Slug = reader.GetString(4),
            Summary = reader.GetString(5),
            ThumbnailUrl = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
            Content = reader.GetString(7),
            CreatedAtUtc = ParseSqliteDateTime(reader.GetString(8)),
            UpdatedAtUtc = reader.IsDBNull(9) ? null : ParseSqliteDateTime(reader.GetString(9)),
            ViewCount = reader.IsDBNull(10) ? 0 : reader.GetInt32(10),
            TagsText = reader.IsDBNull(11) ? string.Empty : reader.GetString(11),
            CategoryId = reader.IsDBNull(12) ? null : reader.GetString(12),
            CategoryName = reader.IsDBNull(13) ? string.Empty : reader.GetString(13),
            CategorySlug = reader.IsDBNull(14) ? string.Empty : reader.GetString(14)
        };
    }

    private static string BuildTagsSelect(string? displayColumn, string articleAlias)
    {
        if (displayColumn is null)
        {
            return "''";
        }

        return $"COALESCE((SELECT GROUP_CONCAT(t.{displayColumn}, ', ') FROM journalist_article_tags jat JOIN tags t ON t.id = jat.tag_id WHERE jat.article_id = {articleAlias}.id), '')";
    }

    private static string BuildCategoryNameSelect(string articleAlias)
    {
        return $"COALESCE((SELECT c.name FROM categories c WHERE c.id = {articleAlias}.category_id LIMIT 1), '')";
    }

    private static string BuildCategorySlugSelect(string articleAlias)
    {
        return $"COALESCE((SELECT c.slug FROM categories c WHERE c.id = {articleAlias}.category_id LIMIT 1), '')";
    }

    private static async Task EnsureViewCountColumnAsync(SqliteConnection connection)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(journalist_articles);";

        var hasViewCount = false;
        await using (var reader = await pragmaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (columnName.Equals("view_count", StringComparison.OrdinalIgnoreCase))
                {
                    hasViewCount = true;
                    break;
                }
            }
        }

        if (!hasViewCount)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE journalist_articles ADD COLUMN view_count INTEGER NOT NULL DEFAULT 0;";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureCategoryIdColumnAsync(SqliteConnection connection)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(journalist_articles);";

        var hasCategoryId = false;
        await using (var reader = await pragmaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (columnName.Equals("category_id", StringComparison.OrdinalIgnoreCase))
                {
                    hasCategoryId = true;
                    break;
                }
            }
        }

        if (!hasCategoryId)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE journalist_articles ADD COLUMN category_id TEXT NULL;";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureThumbnailUrlColumnAsync(SqliteConnection connection)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(journalist_articles);";

        var hasThumbnailUrl = false;
        await using (var reader = await pragmaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (columnName.Equals("thumbnail_url", StringComparison.OrdinalIgnoreCase))
                {
                    hasThumbnailUrl = true;
                    break;
                }
            }
        }

        if (!hasThumbnailUrl)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE journalist_articles ADD COLUMN thumbnail_url TEXT NOT NULL DEFAULT '/images/default-thumb.svg';";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureJournalistArticleTagsTagIdTextAsync(SqliteConnection connection)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(journalist_article_tags);";

        var tagIdType = string.Empty;
        await using (var reader = await pragmaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                if (reader.GetString(1).Equals("tag_id", StringComparison.OrdinalIgnoreCase))
                {
                    tagIdType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                    break;
                }
            }
        }

        if (tagIdType.Equals("TEXT", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();

        await using (var createCommand = connection.CreateCommand())
        {
            createCommand.Transaction = transaction;
            createCommand.CommandText = @"
                CREATE TABLE IF NOT EXISTS journalist_article_tags_new (
                    article_id INTEGER NOT NULL,
                    tag_id TEXT NOT NULL,
                    PRIMARY KEY (article_id, tag_id),
                    FOREIGN KEY(article_id) REFERENCES journalist_articles(id) ON DELETE CASCADE,
                    FOREIGN KEY(tag_id) REFERENCES tags(id)
                );";
            await createCommand.ExecuteNonQueryAsync();
        }

        await using (var copyCommand = connection.CreateCommand())
        {
            copyCommand.Transaction = transaction;
            copyCommand.CommandText = @"
                INSERT OR IGNORE INTO journalist_article_tags_new (article_id, tag_id)
                SELECT jat.article_id, t.id
                FROM journalist_article_tags jat
                JOIN tags t ON CAST(jat.tag_id AS TEXT) = t.id;";
            await copyCommand.ExecuteNonQueryAsync();
        }

        await using (var dropOld = connection.CreateCommand())
        {
            dropOld.Transaction = transaction;
            dropOld.CommandText = "DROP TABLE journalist_article_tags;";
            await dropOld.ExecuteNonQueryAsync();
        }

        await using (var renameNew = connection.CreateCommand())
        {
            renameNew.Transaction = transaction;
            renameNew.CommandText = "ALTER TABLE journalist_article_tags_new RENAME TO journalist_article_tags;";
            await renameNew.ExecuteNonQueryAsync();
        }

        await using (var indexCommand = connection.CreateCommand())
        {
            indexCommand.Transaction = transaction;
            indexCommand.CommandText = @"
                CREATE INDEX IF NOT EXISTS idx_journalist_article_tags_article_id
                    ON journalist_article_tags(article_id);
                CREATE INDEX IF NOT EXISTS idx_journalist_article_tags_tag_id
                    ON journalist_article_tags(tag_id);";
            await indexCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task EnsureSlugColumnAsync(SqliteConnection connection)
    {
        await using var pragmaCommand = connection.CreateCommand();
        pragmaCommand.CommandText = "PRAGMA table_info(journalist_articles);";

        var hasSlug = false;
        await using (var reader = await pragmaCommand.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                var columnName = reader.GetString(1);
                if (columnName.Equals("slug", StringComparison.OrdinalIgnoreCase))
                {
                    hasSlug = true;
                    break;
                }
            }
        }

        if (!hasSlug)
        {
            await using var alterCommand = connection.CreateCommand();
            alterCommand.CommandText = "ALTER TABLE journalist_articles ADD COLUMN slug TEXT NOT NULL DEFAULT '';";
            await alterCommand.ExecuteNonQueryAsync();
        }
    }

    private async Task BackfillMissingSlugsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, title
            FROM journalist_articles
            WHERE slug IS NULL OR trim(slug) = ''
            ORDER BY id;";

        var toUpdate = new List<(long Id, string Title)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                toUpdate.Add((reader.GetInt64(0), reader.GetString(1)));
            }
        }

        if (toUpdate.Count == 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        foreach (var item in toUpdate)
        {
            var slug = await GenerateUniqueSlugAsync(connection, transaction, item.Title, item.Id);

            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE journalist_articles SET slug = $slug WHERE id = $id;";
            updateCommand.Parameters.AddWithValue("$slug", slug);
            updateCommand.Parameters.AddWithValue("$id", item.Id);
            await updateCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private static async Task BackfillMissingThumbnailsAsync(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, content
            FROM journalist_articles
            WHERE thumbnail_url IS NULL OR trim(thumbnail_url) = ''
            ORDER BY id;";

        var toUpdate = new List<(long Id, string Content)>();
        await using (var reader = await command.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                toUpdate.Add((reader.GetInt64(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1)));
            }
        }

        if (toUpdate.Count == 0)
        {
            return;
        }

        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync();
        foreach (var item in toUpdate)
        {
            var extractedUrl = ExtractFirstImageUrl(item.Content);
            var finalUrl = string.IsNullOrWhiteSpace(extractedUrl) ? "/images/default-thumb.svg" : extractedUrl;

            await using var updateCommand = connection.CreateCommand();
            updateCommand.Transaction = transaction;
            updateCommand.CommandText = "UPDATE journalist_articles SET thumbnail_url = $thumbnailUrl WHERE id = $id;";
            updateCommand.Parameters.AddWithValue("$thumbnailUrl", finalUrl);
            updateCommand.Parameters.AddWithValue("$id", item.Id);
            await updateCommand.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    private async Task<string> GenerateUniqueSlugAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string rawTitle,
        long? excludedArticleId)
    {
        var baseSlug = BuildSlug(rawTitle);
        if (string.IsNullOrWhiteSpace(baseSlug))
        {
            baseSlug = "bai-viet";
        }

        var candidate = baseSlug;
        var suffix = 2;

        while (await SlugExistsAsync(connection, transaction, candidate, excludedArticleId))
        {
            candidate = $"{baseSlug}-{suffix}";
            suffix++;
        }

        return candidate;
    }

    private static async Task<bool> SlugExistsAsync(
        SqliteConnection connection,
        SqliteTransaction? transaction,
        string slug,
        long? excludedArticleId)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            SELECT 1
            FROM journalist_articles
            WHERE lower(slug) = lower($slug)
              AND ($excludedId IS NULL OR id <> $excludedId)
            LIMIT 1;";
        command.Parameters.AddWithValue("$slug", slug);
        command.Parameters.AddWithValue("$excludedId", excludedArticleId.HasValue ? excludedArticleId.Value : DBNull.Value);

        var existing = await command.ExecuteScalarAsync();
        return existing is not null;
    }

    private async Task ReplaceArticleTagsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        long articleId,
        IReadOnlyCollection<string> selectedTagIds,
        string newTags)
    {
        var existingSelectedTagIds = await GetExistingTagIdsAsync(connection, transaction, selectedTagIds);
        var allTagIds = new HashSet<string>(existingSelectedTagIds, StringComparer.OrdinalIgnoreCase);
        var parsedNames = ParseTagNames(newTags);

        foreach (var tagName in parsedNames)
        {
            var createdTagId = await GetOrCreateTagAsync(connection, transaction, tagName);
            if (!string.IsNullOrWhiteSpace(createdTagId))
            {
                allTagIds.Add(createdTagId);
            }
        }

        await using var deleteCommand = connection.CreateCommand();
        deleteCommand.Transaction = transaction;
        deleteCommand.CommandText = "DELETE FROM journalist_article_tags WHERE article_id = $articleId;";
        deleteCommand.Parameters.AddWithValue("$articleId", articleId);
        await deleteCommand.ExecuteNonQueryAsync();

        foreach (var tagId in allTagIds)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText = @"
                INSERT OR IGNORE INTO journalist_article_tags (article_id, tag_id)
                VALUES ($articleId, $tagId);";
            insertCommand.Parameters.AddWithValue("$articleId", articleId);
            insertCommand.Parameters.AddWithValue("$tagId", tagId);
            await insertCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<string>> GetExistingTagIdsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        IReadOnlyCollection<string> tagIds)
    {
        var normalized = tagIds
            .Select(tagId => (tagId ?? string.Empty).Trim())
            .Where(tagId => !string.IsNullOrWhiteSpace(tagId))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalized.Count == 0)
        {
            return new List<string>();
        }

        var existingIds = new List<string>();

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        var parameterNames = new List<string>();

        for (var index = 0; index < normalized.Count; index++)
        {
            var parameterName = "$tag" + index;
            parameterNames.Add(parameterName);
            command.Parameters.AddWithValue(parameterName, normalized[index]);
        }

        command.CommandText = $"SELECT id FROM tags WHERE id IN ({string.Join(", ", parameterNames)});";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            existingIds.Add(reader.GetString(0));
        }

        return existingIds;
    }

    private static List<string> ParseTagNames(string tagsInput)
    {
        return tagsInput
            .Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeTagName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<string?> GetOrCreateTagAsync(SqliteConnection connection, SqliteTransaction transaction, string tagName)
    {
        var tagColumns = await GetTagColumnsAsync(connection);
        if (tagColumns.Count == 0)
        {
            return null;
        }

        var displayColumn = GetTagDisplayColumnName(tagColumns);
        if (displayColumn is null)
        {
            return null;
        }

        var existingId = await FindTagIdByNameAsync(connection, transaction, displayColumn, tagName);
        if (!string.IsNullOrWhiteSpace(existingId))
        {
            return existingId;
        }

        var insertColumns = new List<string>();
        var insertParams = new List<string>();
        var values = new Dictionary<string, object?>();

        foreach (var column in tagColumns)
        {
            if (column.IsPrimaryKey)
            {
                continue;
            }

            if (column.Name.Equals(displayColumn, StringComparison.OrdinalIgnoreCase))
            {
                insertColumns.Add(column.Name);
                insertParams.Add("$" + column.Name);
                values[column.Name] = tagName;
                continue;
            }

            if (column.Name.Equals("slug", StringComparison.OrdinalIgnoreCase))
            {
                insertColumns.Add(column.Name);
                insertParams.Add("$" + column.Name);
                values[column.Name] = BuildSlug(tagName);
                continue;
            }

            if (column.Name.Contains("created_at", StringComparison.OrdinalIgnoreCase) ||
                column.Name.Equals("createdAt", StringComparison.OrdinalIgnoreCase))
            {
                insertColumns.Add(column.Name);
                insertParams.Add("$" + column.Name);
                values[column.Name] = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
                continue;
            }

            if (column.NotNull && !column.HasDefault)
            {
                insertColumns.Add(column.Name);
                insertParams.Add("$" + column.Name);
                values[column.Name] = GetFallbackValue(column);
            }
        }

        if (insertColumns.Count == 0)
        {
            return null;
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.Transaction = transaction;
        insertCommand.CommandText = $@"
            INSERT INTO tags ({string.Join(", ", insertColumns)})
            VALUES ({string.Join(", ", insertParams)});
            SELECT last_insert_rowid();";

        foreach (var (columnName, value) in values)
        {
            insertCommand.Parameters.AddWithValue("$" + columnName, value ?? DBNull.Value);
        }

        await insertCommand.ExecuteNonQueryAsync();

        return await FindTagIdByNameAsync(connection, transaction, displayColumn, tagName);
    }

    private static object GetFallbackValue(TagColumnInfo column)
    {
        if (column.Name.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        var type = column.Type.ToUpperInvariant();
        if (type.Contains("INT") || type.Contains("REAL") || type.Contains("NUM"))
        {
            return 0;
        }

        return string.Empty;
    }

    private async Task<string?> FindTagIdByNameAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string displayColumn,
        string tagName)
    {
        var normalizedTarget = NormalizeTagName(tagName);
        if (string.IsNullOrWhiteSpace(normalizedTarget))
        {
            return null;
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
            SELECT id, {displayColumn}
            FROM tags;";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(1))
            {
                continue;
            }

            var existingName = NormalizeTagName(reader.GetString(1));
            if (existingName.Equals(normalizedTarget, StringComparison.OrdinalIgnoreCase))
            {
                return reader.GetString(0);
            }
        }

        return null;
    }

    private async Task<string?> GetTagDisplayColumnAsync(SqliteConnection connection)
    {
        var columns = await GetTagColumnsAsync(connection);
        return GetTagDisplayColumnName(columns);
    }

    private static string? GetTagDisplayColumnName(IEnumerable<TagColumnInfo> columns)
    {
        var names = columns.Select(c => c.Name).ToList();
        var preferred = new[] { "name", "tag_name", "title", "label", "slug" };
        return preferred.FirstOrDefault(candidate => names.Any(name => name.Equals(candidate, StringComparison.OrdinalIgnoreCase)));
    }

    private async Task<List<TagColumnInfo>> GetTagColumnsAsync(SqliteConnection connection)
    {
        var result = new List<TagColumnInfo>();

        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA table_info(tags);";

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new TagColumnInfo
            {
                Name = reader.GetString(1),
                Type = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                NotNull = reader.GetInt32(3) == 1,
                HasDefault = !reader.IsDBNull(4),
                IsPrimaryKey = reader.GetInt32(5) == 1
            });
        }

        return result;
    }

    private static string BuildSlug(string input)
    {
        var chars = input
            .Trim()
            .ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '-')
            .ToArray();

        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug.Trim('-');
    }

    private static string NormalizeTagName(string input)
    {
        var value = (input ?? string.Empty).Trim();
        value = value.TrimStart('#').Trim();
        value = Regex.Replace(value, "\\s+", " ");
        return value.ToLowerInvariant();
    }

    private static string? ExtractFirstImageUrl(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return null;
        }

        var match = Regex.Match(html, "<img[^>]*src\\s*=\\s*['\"](?<src>[^'\"]+)['\"][^>]*>", RegexOptions.IgnoreCase);
        if (!match.Success)
        {
            return null;
        }

        var src = match.Groups["src"].Value.Trim();
        return string.IsNullOrWhiteSpace(src) ? null : src;
    }

    private sealed class TagColumnInfo
    {
        public string Name { get; set; } = string.Empty;

        public string Type { get; set; } = string.Empty;

        public bool NotNull { get; set; }

        public bool HasDefault { get; set; }

        public bool IsPrimaryKey { get; set; }
    }

    private static DateTime ParseSqliteDateTime(string value)
    {
        if (DateTime.TryParse(value, out var parsed))
        {
            return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        return DateTime.UtcNow;
    }
}
