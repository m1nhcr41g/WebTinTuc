using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Articles;

namespace WebTinTuc.Services.Articles;

public class ArticleInteractionStore
{
    public static readonly IReadOnlyList<(string Type, string Label)> ReactionTypes =
    [
        ("like", "Thich"),
        ("love", "Yeu thich"),
        ("wow", "An tuong")
    ];

    private readonly string _connectionString;

    public ArticleInteractionStore(IConfiguration configuration)
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
            CREATE TABLE IF NOT EXISTS journalist_article_comments (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                article_id INTEGER NOT NULL,
                account_id INTEGER NOT NULL,
                author_name TEXT NOT NULL,
                content TEXT NOT NULL,
                created_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                FOREIGN KEY(article_id) REFERENCES journalist_articles(id) ON DELETE CASCADE,
                FOREIGN KEY(account_id) REFERENCES auth_accounts(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_journalist_article_comments_article_id
                ON journalist_article_comments(article_id);

            CREATE TABLE IF NOT EXISTS journalist_article_reactions (
                article_id INTEGER NOT NULL,
                account_id INTEGER NOT NULL,
                reaction_type TEXT NOT NULL,
                updated_at_utc TEXT NOT NULL DEFAULT CURRENT_TIMESTAMP,
                PRIMARY KEY (article_id, account_id),
                FOREIGN KEY(article_id) REFERENCES journalist_articles(id) ON DELETE CASCADE,
                FOREIGN KEY(account_id) REFERENCES auth_accounts(id) ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS idx_journalist_article_reactions_article_id
                ON journalist_article_reactions(article_id);";

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<ArticleCommentViewModel>> GetCommentsAsync(long articleId, int limit = 100)
    {
        var result = new List<ArticleCommentViewModel>();

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT id, author_name, content, created_at_utc
            FROM journalist_article_comments
            WHERE article_id = $articleId
            ORDER BY datetime(created_at_utc) DESC
            LIMIT $limit;";
        command.Parameters.AddWithValue("$articleId", articleId);
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new ArticleCommentViewModel
            {
                Id = reader.GetInt64(0),
                AuthorName = reader.GetString(1),
                Content = reader.GetString(2),
                CreatedAtUtc = ParseSqliteDateTime(reader.GetString(3))
            });
        }

        return result;
    }

    public async Task<Dictionary<string, int>> GetReactionCountsAsync(long articleId)
    {
        var result = ReactionTypes.ToDictionary(item => item.Type, _ => 0, StringComparer.OrdinalIgnoreCase);

        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT reaction_type, COUNT(*)
            FROM journalist_article_reactions
            WHERE article_id = $articleId
            GROUP BY reaction_type;";
        command.Parameters.AddWithValue("$articleId", articleId);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(0);
            var count = reader.GetInt32(1);
            result[type] = count;
        }

        return result;
    }

    public async Task<string?> GetUserReactionAsync(long articleId, long accountId)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT reaction_type
            FROM journalist_article_reactions
            WHERE article_id = $articleId AND account_id = $accountId
            LIMIT 1;";
        command.Parameters.AddWithValue("$articleId", articleId);
        command.Parameters.AddWithValue("$accountId", accountId);

        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    public async Task AddCommentAsync(long articleId, long accountId, string authorName, string content)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO journalist_article_comments (article_id, account_id, author_name, content)
            VALUES ($articleId, $accountId, $authorName, $content);";
        command.Parameters.AddWithValue("$articleId", articleId);
        command.Parameters.AddWithValue("$accountId", accountId);
        command.Parameters.AddWithValue("$authorName", authorName.Trim());
        command.Parameters.AddWithValue("$content", content.Trim());

        await command.ExecuteNonQueryAsync();
    }

    public async Task SetReactionAsync(long articleId, long accountId, string? reactionType)
    {
        await using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        if (string.IsNullOrWhiteSpace(reactionType) || !IsValidReactionType(reactionType))
        {
            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.CommandText = @"
                DELETE FROM journalist_article_reactions
                WHERE article_id = $articleId AND account_id = $accountId;";
            deleteCommand.Parameters.AddWithValue("$articleId", articleId);
            deleteCommand.Parameters.AddWithValue("$accountId", accountId);
            await deleteCommand.ExecuteNonQueryAsync();
            return;
        }

        await using var upsertCommand = connection.CreateCommand();
        upsertCommand.CommandText = @"
            INSERT INTO journalist_article_reactions (article_id, account_id, reaction_type, updated_at_utc)
            VALUES ($articleId, $accountId, $reactionType, CURRENT_TIMESTAMP)
            ON CONFLICT(article_id, account_id)
            DO UPDATE SET
                reaction_type = excluded.reaction_type,
                updated_at_utc = CURRENT_TIMESTAMP;";
        upsertCommand.Parameters.AddWithValue("$articleId", articleId);
        upsertCommand.Parameters.AddWithValue("$accountId", accountId);
        upsertCommand.Parameters.AddWithValue("$reactionType", reactionType.Trim().ToLowerInvariant());

        await upsertCommand.ExecuteNonQueryAsync();
    }

    public static bool IsValidReactionType(string? reactionType)
    {
        if (string.IsNullOrWhiteSpace(reactionType))
        {
            return false;
        }

        return ReactionTypes.Any(item => item.Type.Equals(reactionType.Trim(), StringComparison.OrdinalIgnoreCase));
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
