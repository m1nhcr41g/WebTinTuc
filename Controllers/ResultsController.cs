using Microsoft.AspNetCore.Mvc;

namespace WebTinTuc.Controllers;

public class ResultsController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        return View();
    }
}
