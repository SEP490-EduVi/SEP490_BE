namespace EduVi.Repositories.Models;

public class ExpertSalesAnalyticsRow
{
    public string ExpertCode { get; set; } = string.Empty;
    public string ExpertName { get; set; } = string.Empty;
    public int SoldMaterialCount { get; set; }
    public int SoldCount { get; set; }
    public int UniqueBuyerCount { get; set; }
    public decimal GrossRevenue { get; set; }
    public DateTime? LastPurchasedDate { get; set; }
}
