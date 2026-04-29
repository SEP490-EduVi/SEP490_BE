namespace EduVi.Repositories.Models;

public class MaterialSalesAnalyticsRow
{
    public string MaterialCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? GradeCode { get; set; }
    public string? ExpertCode { get; set; }
    public string? ExpertName { get; set; }
    public int SoldCount { get; set; }
    public int UniqueBuyerCount { get; set; }
    public decimal GrossRevenue { get; set; }
    public DateTime? LastPurchasedDate { get; set; }
}
