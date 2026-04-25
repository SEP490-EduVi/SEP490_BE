namespace EduVi.Contracts.DTOs.Admin.Response;

public class AdminRevenueForecastResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int PeriodDays { get; set; }
    public int ForecastDays { get; set; }
    public decimal CurrentRevenue { get; set; }
    public decimal PreviousRevenue { get; set; }
    public decimal RevenueGrowthRatePercent { get; set; }
    public decimal AverageDailyRevenue { get; set; }
    public decimal ForecastRevenue { get; set; }
    public int CurrentSoldCount { get; set; }
    public int PreviousSoldCount { get; set; }
    public int CurrentUniqueBuyerCount { get; set; }
    public int PreviousUniqueBuyerCount { get; set; }
}
