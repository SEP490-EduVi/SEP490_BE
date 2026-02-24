namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Thông tin gói subscription
/// </summary>
public class SubscriptionPlanResponse
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int QuotaAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
