using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin tạo gói cước mới
/// </summary>
public class CreatePlanRequest
{
    [Required(ErrorMessage = "PlanName is required")]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be >= 0")]
    public decimal Price { get; set; }

    [Required(ErrorMessage = "DurationDays is required")]
    [Range(1, 3650, ErrorMessage = "DurationDays must be between 1 and 3650")]
    public int DurationDays { get; set; }

    [Required(ErrorMessage = "AnalysisQuotaAmount is required")]
    [Range(0, int.MaxValue)]
    public int AnalysisQuotaAmount { get; set; }

    [Required(ErrorMessage = "SlideQuotaAmount is required")]
    [Range(0, int.MaxValue)]
    public int SlideQuotaAmount { get; set; }

    [Required(ErrorMessage = "VideoQuotaAmount is required")]
    [Range(0, int.MaxValue)]
    public int VideoQuotaAmount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }
}
