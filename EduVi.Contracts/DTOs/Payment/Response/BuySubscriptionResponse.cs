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

    public int AnalysisQuotaAdded { get; set; }

    public int SlideQuotaAdded { get; set; }

    public int VideoQuotaAdded { get; set; }

    public int AvailableAnalysisQuotaAfter { get; set; }

    public int AvailableSlideQuotaAfter { get; set; }

    public int AvailableVideoQuotaAfter { get; set; }

    /// <summary>
    /// Số dư ví sau giao dịch
    /// </summary>
    public decimal WalletBalanceAfter { get; set; }

    public DateTime PurchasedAt { get; set; }
}
