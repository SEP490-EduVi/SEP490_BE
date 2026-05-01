using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Bộ lọc đơn hàng cho Admin
/// </summary>
public class OrderFilterRequest
{
    /// <summary>
    /// Lọc theo TeacherId
    /// </summary>
    public int? TeacherId { get; set; }

    /// <summary>
    /// Lọc theo loại đơn: PLAN, MATERIAL
    /// </summary>
    public string? OrderType { get; set; }

    /// <summary>
    /// Lọc theo trạng thái: 0=Pending, 1=Completed, 2=Failed, 3=Cancelled
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Lọc theo phương thức: EduCoin, PayOS, ...
    /// </summary>
    public string? PaymentMethod { get; set; }

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
