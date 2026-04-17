using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace EduVi.Contracts.DTOs.Template;

public class UpdateTemplateRequestDto
{
    [Required(ErrorMessage = "Tên template không được để trống")]
    [MaxLength(200, ErrorMessage = "Tên template không được vượt quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Mô tả không được vượt quá 500 ký tự")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Category không được để trống")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Skeleton không được để trống")]
    public JsonElement Skeleton { get; set; }
}
