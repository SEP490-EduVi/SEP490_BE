using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.TextbookIngestion;

/// <summary>
/// Request DTO cho upload sách giáo khoa (.pdf)
/// </summary>
public class UploadTextbookDocumentRequestDto
{
    /// <summary>File sách giáo khoa (.pdf only)</summary>
    [Required(ErrorMessage = "File is required")]
    public IFormFile File { get; set; } = null!;

    /// <summary>Mã môn học, snake_case không dấu (e.g. "dia_li", "lich_su")</summary>
    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(50)]
    public string SubjectCode { get; set; } = string.Empty;

    /// <summary>Mã lớp học, chỉ số (e.g. "10", "11", "12")</summary>
    [Required(ErrorMessage = "GradeCode is required")]
    [MaxLength(10)]
    public string GradeCode { get; set; } = string.Empty;

    /// <summary>Năm xuất bản sách (e.g. 2022) — tùy chọn</summary>
    public int? PublishYear { get; set; }

    /// <summary>Nhà xuất bản (e.g. "NXB Giáo Dục Việt Nam") — tùy chọn</summary>
    [MaxLength(200)]
    public string? Publisher { get; set; }

    /// <summary>Ghi chú tùy chọn</summary>
    public string? Note { get; set; }
}
