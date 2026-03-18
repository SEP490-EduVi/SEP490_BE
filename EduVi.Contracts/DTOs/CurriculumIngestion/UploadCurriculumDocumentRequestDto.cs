using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.CurriculumIngestion;

/// <summary>
/// Request DTO cho upload tài liệu chương trình giáo dục (.docx)
/// </summary>
public class UploadCurriculumDocumentRequestDto
{
    /// <summary>
    /// File chương trình giáo dục (.docx only)
    /// </summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Mã môn học (e.g. "dia_li") — dùng để tra cứu tên môn học và khớp với Neo4j
    /// </summary>
    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(50)]
    public string SubjectCode { get; set; } = string.Empty;

    /// <summary>
    /// Cấp học: "THPT" hoặc "THCS"
    /// </summary>
    [Required(ErrorMessage = "EducationLevel is required")]
    [MaxLength(20)]
    public string EducationLevel { get; set; } = string.Empty;

    /// <summary>
    /// Năm ban hành chương trình (e.g. 2018)
    /// </summary>
    [Required(ErrorMessage = "CurriculumYear is required")]
    public int CurriculumYear { get; set; }

    /// <summary>
    /// Ghi chú tùy chọn
    /// </summary>
    public string? Note { get; set; }
}
