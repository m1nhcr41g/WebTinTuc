using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using WebTinTuc.Models.Home;
using WebTinTuc.Models;
using WebTinTuc.Services.Articles;

namespace WebTinTuc.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly JournalistArticleStore _articleStore;

        public HomeController(ILogger<HomeController> logger, JournalistArticleStore articleStore)
        {
            _logger = logger;
            _articleStore = articleStore;
        }

        public async Task<IActionResult> Index()
        {
            var viewModel = new HomeDashboardViewModel
            {
                HotArticles = await _articleStore.GetHotArticlesAsync(5),
                LatestArticles = await _articleStore.GetLatestArticlesAsync(6)
            };

            return View(viewModel);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
