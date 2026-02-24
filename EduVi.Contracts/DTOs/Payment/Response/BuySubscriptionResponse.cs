namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Response sau khi mua gói subscription thành công
/// </summary>
public class BuySubscriptionResponse
{
    public int OrderId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Số quota được cộng thêm
    /// </summary>
    public int QuotaAdded { get; set; }

    /// <summary>
    /// Số dư ví sau giao dịch
    /// </summary>
    public decimal WalletBalanceAfter { get; set; }

    public DateTime PurchasedAt { get; set; }
}
