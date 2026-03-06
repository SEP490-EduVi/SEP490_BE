using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Expert upload học liệu dạng FILE: image | video
/// Content-Type: multipart/form-data
/// </summary>
public class UploadFileMaterialRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Thumbnail/preview (optional — nên có cho video)
    /// </summary>
    public IFormFile? PreviewFile { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// image | video
    /// </summary>
    [Required]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Giá bán (VND). Nếu null hoặc 0 thì miễn phí.
    /// </summary>
    public decimal? Price { get; set; }

    public string? SubjectCode { get; set; }

    public string? GradeCode { get; set; }
}
