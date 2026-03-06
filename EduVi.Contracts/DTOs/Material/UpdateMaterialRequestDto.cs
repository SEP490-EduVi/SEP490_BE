using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Expert cập nhật material đã upload (chỉ được sửa khi chưa approve).
/// </summary>
public class UpdateMaterialRequestDto
{
    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    /// <summary>
    /// Giá bán (VND). Nếu null thì giữ nguyên giá cũ.
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
