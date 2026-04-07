namespace WebTinTuc.Models.Articles;

public class ArticleReactionOptionViewModel
{
    public string Type { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public int Count { get; set; }

    public bool IsSelected { get; set; }
}
