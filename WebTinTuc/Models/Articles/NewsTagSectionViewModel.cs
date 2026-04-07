namespace WebTinTuc.Models.Articles;

public class NewsTagSectionViewModel
{
    public string TagName { get; set; } = string.Empty;

    public List<JournalistArticle> Articles { get; set; } = new();
}
