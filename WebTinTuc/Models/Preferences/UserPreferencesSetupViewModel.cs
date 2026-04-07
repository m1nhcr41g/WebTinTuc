using System.ComponentModel.DataAnnotations;

namespace WebTinTuc.Models.Preferences;

public class UserPreferencesSetupViewModel
{
    public List<PreferenceOption> TeamOptions { get; set; } = new();

    public List<PreferenceOption> LeagueOptions { get; set; } = new();

    [Display(Name = "Doi bong yeu thich")]
    public List<long> SelectedTeamIds { get; set; } = new();

    [Display(Name = "Giai dau yeu thich")]
    public List<long> SelectedLeagueIds { get; set; } = new();
}
