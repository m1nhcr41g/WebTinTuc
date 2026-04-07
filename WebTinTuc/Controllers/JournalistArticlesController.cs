using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using WebTinTuc.Models.Articles;
using WebTinTuc.Services.Articles;

namespace WebTinTuc.Controllers;

[Authorize(Roles = "Journalist")]
public class JournalistArticlesController : Controller
{
    private readonly JournalistArticleStore _articleStore;
    private readonly IWebHostEnvironment _environment;

    public JournalistArticlesController(JournalistArticleStore articleStore, IWebHostEnvironment environment)
    {
        _articleStore = articleStore;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var articles = await _articleStore.GetByAuthorAsync(accountId.Value);
        return View(articles);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var model = new ArticleFormViewModel();
        await PopulateFormOptionsAsync(model);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ArticleFormViewModel model)
    {
        model.Content = (model.Content ?? string.Empty).Trim();
        if (IsHtmlContentEmpty(model.Content))
        {
            ModelState.AddModelError(nameof(model.Content), "Vui long nhap noi dung bai viet hoac chen it nhat mot hinh anh.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model);
            return View(model);
        }

        var accountId = GetCurrentAccountId();
        var authorName = User.Identity?.Name;
        if (accountId is null || string.IsNullOrWhiteSpace(authorName))
        {
            return Challenge();
        }

        try
        {
            await _articleStore.CreateAsync(accountId.Value, authorName, model);
        }
        catch (SqliteException)
        {
            ModelState.AddModelError(string.Empty, "Khong the luu bai viet do du lieu tag khong hop le. Vui long bo chon tag va thu lai.");
            await PopulateFormOptionsAsync(model);
            return View(model);
        }

        TempData["SuccessMessage"] = "Dang bai viet thanh cong.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var article = await _articleStore.GetByIdAsync(id, accountId.Value);
        if (article is null)
        {
            return NotFound();
        }

        var model = new ArticleFormViewModel
        {
            Id = article.Id,
            CategoryId = article.CategoryId,
            Title = article.Title,
            Summary = article.Summary,
            Content = article.Content
        };

        model.SelectedTagIds = await _articleStore.GetTagIdsByArticleAsync(id);
        await PopulateFormOptionsAsync(model);

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(long id, ArticleFormViewModel model)
    {
        model.Id = id;
        model.Content = (model.Content ?? string.Empty).Trim();

        if (IsHtmlContentEmpty(model.Content))
        {
            ModelState.AddModelError(nameof(model.Content), "Vui long nhap noi dung bai viet hoac chen it nhat mot hinh anh.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(model);
            return View(model);
        }

        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        bool updated;
        try
        {
            updated = await _articleStore.UpdateAsync(id, accountId.Value, model);
        }
        catch (SqliteException)
        {
            ModelState.AddModelError(string.Empty, "Khong the cap nhat bai viet do du lieu tag khong hop le. Vui long bo chon tag va thu lai.");
            await PopulateFormOptionsAsync(model);
            return View(model);
        }

        if (!updated)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = "Cap nhat bai viet thanh cong.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Delete(long id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var article = await _articleStore.GetByIdAsync(id, accountId.Value);
        if (article is null)
        {
            return NotFound();
        }

        return View(article);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(long id)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var deleted = await _articleStore.DeleteAsync(id, accountId.Value);
        if (!deleted)
        {
            return NotFound();
        }

        TempData["SuccessMessage"] = "Da xoa bai viet.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadImage(IFormFile? image)
    {
        if (image is null || image.Length == 0)
        {
            return BadRequest(new { message = "Vui long chon hinh anh." });
        }

        var maxSize = 5 * 1024 * 1024;
        if (image.Length > maxSize)
        {
            return BadRequest(new { message = "Kich thuoc anh toi da la 5MB." });
        }

        var extension = Path.GetExtension(image.FileName).ToLowerInvariant();
        var allowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".gif", ".webp"
        };

        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { message = "Dinh dang anh khong hop le." });
        }

        var uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads", "articles");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsFolder, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await image.CopyToAsync(stream);
        }

        var imageUrl = Url.Content($"~/uploads/articles/{fileName}");
        return Json(new { url = imageUrl });
    }

    private long? GetCurrentAccountId()
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(accountIdClaim, out var accountId) ? accountId : null;
    }

    private async Task PopulateFormOptionsAsync(ArticleFormViewModel model)
    {
        model.AvailableCategories = await _articleStore.GetAllCategoriesAsync();
        model.AvailableTags = await _articleStore.GetAllTagsAsync();
    }

    private static bool IsHtmlContentEmpty(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return true;
        }

        if (html.Contains("<img", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var stripped = Regex.Replace(html, "<[^>]*>", string.Empty);
        stripped = System.Net.WebUtility.HtmlDecode(stripped).Replace("\u00A0", " ").Trim();
        return string.IsNullOrWhiteSpace(stripped);
    }
}
