namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Lịch sử giao dịch ví
/// </summary>
public class TransactionHistoryResponse
{
    public int TransactionId { get; set; }

    /// <summary>
    /// Mã đơn hàng PayOS (cho top-up) hoặc mã nội bộ
    /// </summary>
    public long OrderCode { get; set; }

    /// <summary>
    /// Tên loại giao dịch tiếng Việt để hiển thị.
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;

    /// <summary>
    /// Mã loại giao dịch trong database (TOP_UP, BUY_SUBSCRIPTION, ...).
    /// </summary>
    public string TransactionTypeCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>
    /// Số dư trước giao dịch
    /// </summary>
    public decimal BalanceBefore { get; set; }

    /// <summary>
    /// Số dư sau giao dịch
    /// </summary>
    public decimal BalanceAfter { get; set; }

    /// <summary>
    /// PENDING | COMPLETED | FAILED | CANCELLED
    /// </summary>
    public string Status { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Tên gói subscription (chỉ có khi TransactionType = BUY_SUBSCRIPTION)
    /// </summary>
    public string? PlanName { get; set; }

    /// <summary>
    /// Tiêu đề tài liệu đã mua (chỉ có khi TransactionType = BUY_MATERIAL)
    /// </summary>
    public string? MaterialTitle { get; set; }

    public DateTime CreatedAt { get; set; }
}
