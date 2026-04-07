namespace WebTinTuc.Models.Articles;

public class ArticleCommentViewModel
{
    public long Id { get; set; }

    public string AuthorName { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }
}
