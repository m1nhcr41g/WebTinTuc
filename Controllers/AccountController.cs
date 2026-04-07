using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebTinTuc.Models.Auth;
using WebTinTuc.Services;

namespace WebTinTuc.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly AuthAccountStore _accountStore;
    private readonly IWebHostEnvironment _environment;
    private static readonly HashSet<string> AllowedAvatarExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };
    private const long MaxAvatarFileSize = 2 * 1024 * 1024;

    public AccountController(AuthAccountStore accountStore, IWebHostEnvironment environment)
    {
        _accountStore = accountStore;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Manage()
    {
        var account = await GetCurrentAccountAsync();
        if (account is null)
        {
            return await ForceLogoutAndRedirectAsync();
        }

        var model = new ManageAccountViewModel
        {
            FullName = account.FullName,
            Email = account.Email,
            Role = account.Role,
            CurrentAvatarPath = account.AvatarPath
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Manage(ManageAccountViewModel model)
    {
        var account = await GetCurrentAccountAsync();
        if (account is null)
        {
            return await ForceLogoutAndRedirectAsync();
        }

        model.Role = account.Role;
        model.CurrentAvatarPath = account.AvatarPath;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        if (await _accountStore.EmailExistsForOtherAccountAsync(model.Email, account.Id))
        {
            ModelState.AddModelError(nameof(model.Email), "Email da ton tai.");
            return View(model);
        }

        string? newPasswordHash = null;
        if (!string.IsNullOrWhiteSpace(model.NewPassword))
        {
            if (!PasswordHasher.VerifyPassword(model.CurrentPassword ?? string.Empty, account.PasswordHash))
            {
                ModelState.AddModelError(nameof(model.CurrentPassword), "Mat khau hien tai khong dung.");
                return View(model);
            }

            newPasswordHash = PasswordHasher.HashPassword(model.NewPassword);
        }

        var avatarPath = account.AvatarPath;
        if (model.AvatarFile is not null && model.AvatarFile.Length > 0)
        {
            var extension = Path.GetExtension(model.AvatarFile.FileName);
            if (!AllowedAvatarExtensions.Contains(extension))
            {
                ModelState.AddModelError(nameof(model.AvatarFile), "Chi chap nhan file .jpg, .jpeg, .png hoac .webp.");
                return View(model);
            }

            if (model.AvatarFile.Length > MaxAvatarFileSize)
            {
                ModelState.AddModelError(nameof(model.AvatarFile), "Avatar toi da 2MB.");
                return View(model);
            }

            avatarPath = await SaveAvatarAsync(model.AvatarFile);
            DeleteOldAvatarIfExists(account.AvatarPath, avatarPath);
        }

        var updated = await _accountStore.UpdateAccountAsync(
            account.Id,
            model.FullName,
            model.Email,
            avatarPath,
            newPasswordHash);

        if (!updated)
        {
            ModelState.AddModelError(string.Empty, "Cap nhat tai khoan that bai. Vui long thu lai.");
            return View(model);
        }

        var refreshedAccount = await _accountStore.GetByIdAsync(account.Id);
        if (refreshedAccount is not null)
        {
            await RefreshSignInAsync(refreshedAccount);
        }

        TempData["SuccessMessage"] = "Cap nhat tai khoan thanh cong.";
        return RedirectToAction(nameof(Manage));
    }

    private async Task<AuthAccount?> GetCurrentAccountAsync()
    {
        if (!long.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var accountId))
        {
            return null;
        }

        return await _accountStore.GetByIdAsync(accountId);
    }

    private async Task<IActionResult> ForceLogoutAndRedirectAsync()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Auth");
    }

    private async Task RefreshSignInAsync(AuthAccount account)
    {
        var authResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        var properties = authResult.Succeeded
            ? authResult.Properties
            : new AuthenticationProperties
            {
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(1)
            };

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.FullName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role)
        };

        if (!string.IsNullOrWhiteSpace(account.AvatarPath))
        {
            claims.Add(new Claim("avatar_path", account.AvatarPath));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }

    private async Task<string> SaveAvatarAsync(IFormFile avatarFile)
    {
        var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "avatars");
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(avatarFile.FileName);
        var fileName = $"avatar_{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(uploadsRoot, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await avatarFile.CopyToAsync(stream);

        return $"/uploads/avatars/{fileName}";
    }

    private void DeleteOldAvatarIfExists(string? oldAvatarPath, string newAvatarPath)
    {
        if (string.IsNullOrWhiteSpace(oldAvatarPath) || string.Equals(oldAvatarPath, newAvatarPath, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!oldAvatarPath.StartsWith("/uploads/avatars/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var relativePath = oldAvatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(_environment.WebRootPath, relativePath);
        if (System.IO.File.Exists(physicalPath))
        {
            System.IO.File.Delete(physicalPath);
        }
    }
}
