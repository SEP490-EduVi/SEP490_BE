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
    /// TOP_UP | BUY_SUBSCRIPTION
    /// </summary>
    public string TransactionType { get; set; } = string.Empty;

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
    public DateTime CreatedAt { get; set; }
}
