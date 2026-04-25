using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Expert;

public class ExpertSalesFilterRequest
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? SubjectCode { get; set; }
    public string? GradeCode { get; set; }
    public string? MaterialCode { get; set; }

    [Range(1, 365)]
    public int ForecastDays { get; set; } = 30;
}
