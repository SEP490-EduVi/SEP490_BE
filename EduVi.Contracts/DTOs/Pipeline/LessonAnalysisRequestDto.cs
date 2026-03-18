using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho phân tích bài giảng — chọn InputDocument đã upload sẵn
/// </summary>
public class LessonAnalysisRequestDto
{
    /// <summary>
    /// Code của InputDocument đã upload trước đó
    /// </summary>
    [Required(ErrorMessage = "DocumentCode is required")]
    [MaxLength(100)]
    public string DocumentCode { get; set; } = string.Empty;

    /// <summary>
    /// Code của Project chứa Product sẽ được tạo
    /// </summary>
    [Required(ErrorMessage = "ProjectCode is required")]
    [MaxLength(100)]
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// Tên Product (tùy chọn, nếu không truyền sẽ tự generate từ tên document)
    /// </summary>
    [MaxLength(200)]
    public string? ProductName { get; set; }

    /// <summary>
    /// Năm curriculum tham chiếu (tùy chọn) — e.g. 2018
    /// </summary>
    public int? CurriculumYear { get; set; }
}
