using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using WebTinTuc.Services;

public class AdminController : Controller
{
    private readonly AdminAuthService _auth;

    public AdminController(AdminAuthService auth)
    {
        _auth = auth;
    }

    public IActionResult Login() => View();

    [HttpPost]
    public async Task<IActionResult> Login(string email, string password)
    {
        if (await _auth.Login(email, password))
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, email),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, "AdminScheme");

            await HttpContext.SignInAsync("AdminScheme",
                new ClaimsPrincipal(identity));

            return RedirectToAction("Dashboard");
        }

        ViewBag.Error = "Sai tài khoản hoặc mật khẩu";
        return View();
    }

    public IActionResult Register() => View();
    [HttpPost]
    public async Task<IActionResult> Register(string email, string password)
    {
        var result = await _auth.Register(email, password);

        if (!result)
        {
            ViewBag.Error = "Email đã tồn tại!";
            return View();
        }

        // ✅ đăng ký thành công → về login
        return RedirectToAction("Login");
    }

    // 🔒 BẢO VỆ
    [Authorize(AuthenticationSchemes = "AdminScheme")]
    public IActionResult Dashboard()
    {
        return View();
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync("AdminScheme");
        return RedirectToAction("Login");
    }
}