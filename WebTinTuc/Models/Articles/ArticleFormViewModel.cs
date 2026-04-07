using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebTinTuc.Models.Articles;

public class ArticleFormViewModel
{
    public long? Id { get; set; }

    [Display(Name = "Chuyen muc")]
    public string? CategoryId { get; set; }

    [Display(Name = "Tieu de")]
    [Required(ErrorMessage = "Vui long nhap tieu de")]
    [StringLength(200, ErrorMessage = "Tieu de toi da 200 ky tu")]
    public string Title { get; set; } = string.Empty;

    [Display(Name = "Tom tat")]
    [StringLength(500, ErrorMessage = "Tom tat toi da 500 ky tu")]
    public string? Summary { get; set; }

    [Display(Name = "Anh thumbnail")]
    [RegularExpression("^(https?://|/).+", ErrorMessage = "Link thumbnail khong hop le")]
    [StringLength(600, ErrorMessage = "Link thumbnail toi da 600 ky tu")]
    public string ThumbnailUrl { get; set; } = string.Empty;

    [Display(Name = "Tai anh thumbnail")]
    public IFormFile? ThumbnailFile { get; set; }

    [Display(Name = "Noi dung")]
    [Required(ErrorMessage = "Vui long nhap noi dung")]
    public string Content { get; set; } = string.Empty;

    [Display(Name = "Tags co san")]
    public List<string> SelectedTagIds { get; set; } = new();

    [Display(Name = "Them tags moi")]
    [StringLength(300, ErrorMessage = "Danh sach tags moi toi da 300 ky tu")]
    public string? NewTags { get; set; }

    public List<CategoryOption> AvailableCategories { get; set; } = new();

    public List<TagOption> AvailableTags { get; set; } = new();
}
