using System.ComponentModel.DataAnnotations;

namespace WebTinTuc.Models.Auth;

public class RegisterViewModel
{
    [Display(Name = "Ho ten")]
    [Required(ErrorMessage = "Vui long nhap ho ten")]
    [StringLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui long nhap email")]
    [EmailAddress(ErrorMessage = "Email khong hop le")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Mat khau")]
    [Required(ErrorMessage = "Vui long nhap mat khau")]
    [DataType(DataType.Password)]
    [MinLength(6, ErrorMessage = "Mat khau toi thieu 6 ky tu")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Xac nhan mat khau")]
    [Required(ErrorMessage = "Vui long xac nhan mat khau")]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Xac nhan mat khau khong dung")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [Display(Name = "Loai tai khoan")]
    [Required(ErrorMessage = "Vui long chon loai tai khoan")]
    [RegularExpression("^(User|Journalist)$", ErrorMessage = "Loai tai khoan khong hop le")]
    public string Role { get; set; } = "User";
}
