using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Bộ lọc danh sách người dùng cho Admin
/// </summary>
public class UserFilterRequest
{
    /// <summary>
    /// Lọc theo RoleId (1=Admin, 2=Teacher, 3=Expert, 4=Staff, ...)
    /// </summary>
    public int? RoleId { get; set; }

    /// <summary>
    /// Lọc theo trạng thái: 0=Banned, 1=Active
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Tìm theo tên, email hoặc username
    /// </summary>
    public string? Search { get; set; }

    /// <summary>
    /// Lọc user đăng ký từ ngày
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Lọc user đăng ký đến ngày
    /// </summary>
    public DateTime? ToDate { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
