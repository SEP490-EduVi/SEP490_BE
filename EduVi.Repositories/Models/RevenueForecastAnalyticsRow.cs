namespace EduVi.Repositories.Models;

public class RevenueForecastAnalyticsRow
{
    public decimal CurrentRevenue { get; set; }
    public decimal PreviousRevenue { get; set; }
    public int CurrentSoldCount { get; set; }
    public int PreviousSoldCount { get; set; }
    public int CurrentUniqueBuyerCount { get; set; }
    public int PreviousUniqueBuyerCount { get; set; }
}
