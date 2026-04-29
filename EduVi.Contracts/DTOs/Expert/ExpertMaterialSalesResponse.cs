namespace EduVi.Contracts.DTOs.Expert;

public class ExpertMaterialSalesResponse
{
    public string MaterialCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? GradeCode { get; set; }
    public int SoldCount { get; set; }
    public int UniqueBuyerCount { get; set; }
    public decimal GrossRevenue { get; set; }
    public DateTime? LastPurchasedDate { get; set; }
}
