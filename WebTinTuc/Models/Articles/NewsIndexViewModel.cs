namespace WebTinTuc.Models.Articles;

public class NewsIndexViewModel
{
    public string? SelectedCategorySlug { get; set; }

    public string? SelectedCategoryName { get; set; }

    public List<JournalistArticle> HotArticles { get; set; } = new();

    public List<JournalistArticle> LatestArticles { get; set; } = new();

    public List<NewsCategorySectionViewModel> CategorySections { get; set; } = new();

    public List<JournalistArticle> RecommendedArticles { get; set; } = new();

    public List<string> PreferenceKeywords { get; set; } = new();
}
