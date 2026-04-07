namespace WebTinTuc.Models.Articles;

public class NewsDetailsViewModel
{
    public JournalistArticle Article { get; set; } = new();

    public List<ArticleReactionOptionViewModel> Reactions { get; set; } = new();

    public List<ArticleCommentViewModel> Comments { get; set; } = new();

    public bool CanInteract { get; set; }

    public string NewComment { get; set; } = string.Empty;
}
