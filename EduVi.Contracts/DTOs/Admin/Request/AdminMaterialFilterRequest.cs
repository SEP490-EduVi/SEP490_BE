using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Bộ lọc danh sách học liệu cho Admin.
/// </summary>
public class AdminMaterialFilterRequest
{
    /// <summary>
    /// Lọc theo trạng thái duyệt: 0 = Pending, 1 = Approved, 2 = Rejected.
    /// </summary>
    [Range(0, 2)]
    public int? ApprovalStatus { get; set; }

    /// <summary>
    /// Lọc theo loại học liệu (image, video, ...).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Lọc theo mã môn học.
    /// </summary>
    public string? SubjectCode { get; set; }

    /// <summary>
    /// Lọc theo mã khối lớp.
    /// </summary>
    public string? GradeCode { get; set; }

    /// <summary>
    /// Lọc theo mã chuyên gia sở hữu học liệu.
    /// </summary>
    public string? ExpertCode { get; set; }

    /// <summary>
    /// Tìm theo MaterialCode hoặc Title.
    /// </summary>
    public string? Search { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
