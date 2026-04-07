using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebTinTuc.Models.Articles;
using WebTinTuc.Services.Articles;
using WebTinTuc.Services.Preferences;

namespace WebTinTuc.Controllers;

public class NewsController : Controller
{
    private readonly JournalistArticleStore _articleStore;
    private readonly ArticleInteractionStore _interactionStore;
    private readonly UserPreferenceStore _preferenceStore;

    public NewsController(
        JournalistArticleStore articleStore,
        ArticleInteractionStore interactionStore,
        UserPreferenceStore preferenceStore)
    {
        _articleStore = articleStore;
        _interactionStore = interactionStore;
        _preferenceStore = preferenceStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? category)
    {
        var categorySections = await _articleStore.GetCategorySectionsAsync(6, category);

        var viewModel = new NewsIndexViewModel
        {
            SelectedCategorySlug = string.IsNullOrWhiteSpace(category) ? null : category.Trim(),
            SelectedCategoryName = categorySections.FirstOrDefault()?.CategoryName,
            HotArticles = await _articleStore.GetHotArticlesAsync(6),
            LatestArticles = await _articleStore.GetLatestArticlesAsync(8),
            CategorySections = categorySections
        };

        if (User.Identity?.IsAuthenticated == true && User.IsInRole("User"))
        {
            var accountId = GetCurrentAccountId();
            if (accountId.HasValue)
            {
                var keywords = await _preferenceStore.GetPreferenceKeywordsAsync(accountId.Value);
                viewModel.PreferenceKeywords = keywords;
                viewModel.RecommendedArticles = await _articleStore.GetRecommendedArticlesAsync(keywords, 6);
            }
        }

        return View(viewModel);
    }

    [HttpGet("news/{slug}")]
    public async Task<IActionResult> Details(string slug)
    {
        var article = await _articleStore.GetPublicBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        await _articleStore.IncreaseViewCountAsync(article.Id);
        article.ViewCount += 1;

        var accountId = GetCurrentAccountId();
        var userReaction = accountId.HasValue
            ? await _interactionStore.GetUserReactionAsync(article.Id, accountId.Value)
            : null;

        var reactionCounts = await _interactionStore.GetReactionCountsAsync(article.Id);
        var comments = await _interactionStore.GetCommentsAsync(article.Id);

        var viewModel = new NewsDetailsViewModel
        {
            Article = article,
            Comments = comments,
            CanInteract = User.Identity?.IsAuthenticated == true && User.IsInRole("User"),
            Reactions = ArticleInteractionStore.ReactionTypes
                .Select(item => new ArticleReactionOptionViewModel
                {
                    Type = item.Type,
                    Label = item.Label,
                    Count = reactionCounts.TryGetValue(item.Type, out var count) ? count : 0,
                    IsSelected = item.Type.Equals(userReaction, StringComparison.OrdinalIgnoreCase)
                })
                .ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    public async Task<IActionResult> DetailsById(long id)
    {
        var article = await _articleStore.GetPublicByIdAsync(id);
        if (article is null)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { slug = article.Slug });
    }

    [Authorize(Roles = "User")]
    [HttpPost("news/{slug}/comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddComment(string slug, string? newComment)
    {
        var article = await _articleStore.GetPublicBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        var accountId = GetCurrentAccountId();
        var authorName = User.Identity?.Name;
        if (!accountId.HasValue || string.IsNullOrWhiteSpace(authorName))
        {
            return Challenge();
        }

        var content = (newComment ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(content))
        {
            TempData["CommentError"] = "Vui long nhap noi dung binh luan.";
            return RedirectToAction(nameof(Details), new { slug = article.Slug });
        }

        await _interactionStore.AddCommentAsync(article.Id, accountId.Value, authorName, content);
        return RedirectToAction(nameof(Details), new { slug = article.Slug });
    }

    [Authorize(Roles = "User")]
    [HttpPost("news/{slug}/react")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> React(string slug, string? reactionType)
    {
        var article = await _articleStore.GetPublicBySlugAsync(slug);
        if (article is null)
        {
            return NotFound();
        }

        var accountId = GetCurrentAccountId();
        if (!accountId.HasValue)
        {
            return Challenge();
        }

        var normalized = (reactionType ?? string.Empty).Trim().ToLowerInvariant();
        var existing = await _interactionStore.GetUserReactionAsync(article.Id, accountId.Value);

        if (!string.IsNullOrWhiteSpace(existing) && existing.Equals(normalized, StringComparison.OrdinalIgnoreCase))
        {
            await _interactionStore.SetReactionAsync(article.Id, accountId.Value, null);
        }
        else
        {
            await _interactionStore.SetReactionAsync(article.Id, accountId.Value, normalized);
        }

        return RedirectToAction(nameof(Details), new { slug = article.Slug });
    }

    private long? GetCurrentAccountId()
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(accountIdClaim, out var accountId) ? accountId : null;
    }
}
