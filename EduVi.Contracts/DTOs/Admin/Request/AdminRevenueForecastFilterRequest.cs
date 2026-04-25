using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

public class AdminRevenueForecastFilterRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SubjectCode { get; set; }
    public string? GradeCode { get; set; }
    public string? ExpertCode { get; set; }
    public string? MaterialCode { get; set; }

    [Range(1, 365)]
    public int ForecastDays { get; set; } = 30;
}
