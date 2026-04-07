using WebTinTuc.Models.Articles;

namespace WebTinTuc.Models.Home;

public class HomeDashboardViewModel
{
    public List<JournalistArticle> HotArticles { get; set; } = new();

    public List<JournalistArticle> LatestArticles { get; set; } = new();
}
