using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using WebTinTuc.Models.Auth;
using WebTinTuc.Services;

namespace WebTinTuc.Controllers;

public class AuthController : Controller
{
    private readonly AuthAccountStore _accountStore;

    public AuthController(AuthAccountStore accountStore)
    {
        _accountStore = accountStore;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _accountStore.ValidateCredentialsAsync(model.Email, model.Password);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoac mat khau khong dung.");
            return View(model);
        }

        await SignInAsync(account.Id, account.FullName, account.Email, account.Role, account.AvatarPath, model.RememberMe);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View(new RegisterViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _accountStore.EmailExistsAsync(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Email da ton tai.");
            return View(model);
        }

        var accountId = await _accountStore.CreateAsync(model);

        if (model.Role == "User")
        {
            await SignInAsync(accountId, model.FullName.Trim(), model.Email.Trim(), model.Role, null, false);
            return RedirectToAction("Setup", "UserPreferences");
        }

        TempData["SuccessMessage"] = "Dang ky thanh cong. Vui long dang nhap.";
        return RedirectToAction(nameof(Login));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInAsync(long accountId, string fullName, string email, string role, string? avatarPath, bool rememberMe)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, accountId.ToString()),
            new(ClaimTypes.Name, fullName),
            new(ClaimTypes.Email, email),
            new(ClaimTypes.Role, role)
        };

        if (!string.IsNullOrWhiteSpace(avatarPath))
        {
            claims.Add(new Claim("avatar_path", avatarPath));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(rememberMe ? 14 : 1)
            });
    }
}
