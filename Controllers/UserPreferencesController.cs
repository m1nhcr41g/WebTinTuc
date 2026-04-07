using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebTinTuc.Models.Preferences;
using WebTinTuc.Services.Preferences;

namespace WebTinTuc.Controllers;

[Authorize(Roles = "User")]
public class UserPreferencesController : Controller
{
    private readonly UserPreferenceStore _preferenceStore;

    public UserPreferencesController(UserPreferenceStore preferenceStore)
    {
        _preferenceStore = preferenceStore;
    }

    [HttpGet]
    public async Task<IActionResult> Setup()
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        var model = await BuildViewModelAsync(accountId.Value);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setup(UserPreferencesSetupViewModel model)
    {
        var accountId = GetCurrentAccountId();
        if (accountId is null)
        {
            return Challenge();
        }

        await _preferenceStore.SaveUserPreferencesAsync(
            accountId.Value,
            model.SelectedTeamIds,
            model.SelectedLeagueIds);

        TempData["SuccessMessage"] = "Da luu so thich cua ban.";
        return RedirectToAction("Index", "News");
    }

    private async Task<UserPreferencesSetupViewModel> BuildViewModelAsync(long accountId)
    {
        var model = new UserPreferencesSetupViewModel
        {
            TeamOptions = await _preferenceStore.GetTeamOptionsAsync(),
            LeagueOptions = await _preferenceStore.GetLeagueOptionsAsync()
        };

        var selected = await _preferenceStore.GetSelectedPreferenceIdsAsync(accountId);
        model.SelectedTeamIds = selected.TeamIds;
        model.SelectedLeagueIds = selected.LeagueIds;

        return model;
    }

    private long? GetCurrentAccountId()
    {
        var accountIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return long.TryParse(accountIdClaim, out var accountId) ? accountId : null;
    }
}
