namespace EduVi.Contracts.DTOs.Admin.Response;

/// <summary>
/// Thông tin giao dịch chi tiết cho Admin (bao gồm thông tin user)
/// </summary>
public class AdminTransactionResponse
{
    public int TransactionId { get; set; }
    public long OrderCode { get; set; }

    // User info
    public int? WalletId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }

    /// <summary>
    /// Tên loại giao dịch tiếng Việt để hiển thị.
    /// </summary>
    public string? TransactionType { get; set; }

    /// <summary>
    /// Mã loại giao dịch trong database (TOP_UP, BUY_SUBSCRIPTION, ...).
    /// </summary>
    public string? TransactionTypeCode { get; set; }

    public decimal? Amount { get; set; }
    public decimal? BalanceBefore { get; set; }
    public decimal? BalanceAfter { get; set; }

    public int? Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    public string? Description { get; set; }
    public int? PlanId { get; set; }
    public string? PlanName { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
