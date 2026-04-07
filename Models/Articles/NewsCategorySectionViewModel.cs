namespace WebTinTuc.Models.Articles;

public class NewsCategorySectionViewModel
{
    public string CategoryId { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string CategorySlug { get; set; } = string.Empty;

    public long TotalViewCount { get; set; }

    public List<JournalistArticle> Articles { get; set; } = new();
}
