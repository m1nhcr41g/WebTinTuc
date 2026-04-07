using Microsoft.AspNetCore.Mvc;

namespace WebTinTuc.Controllers;

public class StandingsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
