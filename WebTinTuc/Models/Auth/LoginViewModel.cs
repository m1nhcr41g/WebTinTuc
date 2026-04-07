using System.ComponentModel.DataAnnotations;

namespace WebTinTuc.Models.Auth;

public class LoginViewModel
{
    [Display(Name = "Email")]
    [Required(ErrorMessage = "Vui long nhap email")]
    [EmailAddress(ErrorMessage = "Email khong hop le")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Mat khau")]
    [Required(ErrorMessage = "Vui long nhap mat khau")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Ghi nho dang nhap")]
    public bool RememberMe { get; set; }
}
