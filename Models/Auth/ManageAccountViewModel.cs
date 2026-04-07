using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebTinTuc.Models.Auth;

public class ManageAccountViewModel : IValidatableObject
{
    [Display(Name = "Ho ten")]
    [Required(ErrorMessage = "Vui long nhap ho ten")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui long nhap email")]
    [EmailAddress(ErrorMessage = "Email khong hop le")]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string? CurrentAvatarPath { get; set; }

    [Display(Name = "Avatar")]
    public IFormFile? AvatarFile { get; set; }

    [Display(Name = "Mat khau hien tai")]
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Display(Name = "Mat khau moi")]
    [DataType(DataType.Password)]
    public string? NewPassword { get; set; }

    [Display(Name = "Xac nhan mat khau moi")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Xac nhan mat khau moi khong dung")]
    public string? ConfirmNewPassword { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var hasCurrent = !string.IsNullOrWhiteSpace(CurrentPassword);
        var hasNew = !string.IsNullOrWhiteSpace(NewPassword);
        var hasConfirm = !string.IsNullOrWhiteSpace(ConfirmNewPassword);

        if (hasNew || hasConfirm)
        {
            if (!hasCurrent)
            {
                yield return new ValidationResult("Vui long nhap mat khau hien tai de doi mat khau.", new[] { nameof(CurrentPassword) });
            }

            if (!hasNew)
            {
                yield return new ValidationResult("Vui long nhap mat khau moi.", new[] { nameof(NewPassword) });
            }
            else if (NewPassword!.Length < 6)
            {
                yield return new ValidationResult("Mat khau moi toi thieu 6 ky tu", new[] { nameof(NewPassword) });
            }

            if (!hasConfirm)
            {
                yield return new ValidationResult("Vui long xac nhan mat khau moi.", new[] { nameof(ConfirmNewPassword) });
            }
        }
    }
}
