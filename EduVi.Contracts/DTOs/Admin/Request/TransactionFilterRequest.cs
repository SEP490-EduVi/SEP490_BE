using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Bộ lọc giao dịch cho Admin (tất cả user, không chỉ user hiện tại)
/// </summary>
public class TransactionFilterRequest
{
    /// <summary>
    /// Lọc theo UserId cụ thể
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// Lọc theo mã loại giao dịch: TOP_UP, BUY_SUBSCRIPTION, BUY_MATERIAL, MATERIAL_REVENUE,
    /// MATERIAL_PLATFORM_FEE, MATERIAL_ADMIN_REVENUE, CLAIM_FREE_MATERIAL, WITHDRAWAL
    /// </summary>
    public string? TransactionType { get; set; }

    /// <summary>
    /// Lọc theo trạng thái: 0=Pending, 1=Completed, 2=Failed, 3=Cancelled
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Giao dịch từ ngày
    /// </summary>
    public DateTime? FromDate { get; set; }

    /// <summary>
    /// Giao dịch đến ngày
    /// </summary>
    public DateTime? ToDate { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
