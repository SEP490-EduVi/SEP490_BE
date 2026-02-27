using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho upload InputDocument — file được lưu vào GCS, metadata vào DB
/// </summary>
public class UploadInputDocumentRequestDto
{
    /// <summary>
    /// File bài giảng (PDF, DOCX, PPTX, v.v.)
    /// </summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Tiêu đề tài liệu
    /// </summary>
    [Required(ErrorMessage = "Title is required")]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Code môn học
    /// </summary>
    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(20)]
    public string SubjectCode { get; set; } = string.Empty;

    /// <summary>
    /// Code khối lớp
    /// </summary>
    [Required(ErrorMessage = "GradeCode is required")]
    [MaxLength(50)]
    public string GradeCode { get; set; } = string.Empty;

    /// <summary>
    /// Code bài học (tùy chọn)
    /// </summary>
    [MaxLength(50)]
    public string? LessonCode { get; set; }
}
