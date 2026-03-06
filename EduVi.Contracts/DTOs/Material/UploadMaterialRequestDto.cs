using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Expert upload material mới (hình ảnh, video, document, audio, quiz, flashcard, lesson_plan).
/// </summary>
public class UploadMaterialRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// Loại material: image | video | audio | document | quiz | flashcard | lesson_plan
    /// </summary>
    [Required]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Giá bán (VND). Nếu null hoặc 0 thì miễn phí.
    /// </summary>
    public decimal? Price { get; set; }

    /// <summary>
    /// SubjectCode — môn học liên quan (optional)
    /// </summary>
    public string? SubjectCode { get; set; }

    /// <summary>
    /// GradeCode — khối lớp liên quan (optional)
    /// </summary>
    public string? GradeCode { get; set; }
}
