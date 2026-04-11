namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Thông tin gói subscription
/// </summary>
public class QuotaPlanResponse
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int AnalysisQuotaAmount { get; set; }
    public int SlideQuotaAmount { get; set; }
    public int VideoQuotaAmount { get; set; }
    public int GameQuotaAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
