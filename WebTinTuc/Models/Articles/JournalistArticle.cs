namespace WebTinTuc.Models.Articles;

public class JournalistArticle
{
    public long Id { get; set; }

    public long AuthorAccountId { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Slug { get; set; } = string.Empty;

    public string? CategoryId { get; set; }

    public string CategoryName { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public string ThumbnailUrl { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public int ViewCount { get; set; }

    public string TagsText { get; set; } = string.Empty;
}
