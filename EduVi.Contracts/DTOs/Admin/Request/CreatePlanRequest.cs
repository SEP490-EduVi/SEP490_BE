using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin tạo gói quota mới. Ít nhất 1 loại quota phải > 0.
/// </summary>
public class CreatePlanRequest : IValidatableObject
{
    [Required(ErrorMessage = "PlanName is required")]
    [MaxLength(100)]
    public string PlanName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Price is required")]
    [Range(0, double.MaxValue, ErrorMessage = "Price must be >= 0")]
    public decimal Price { get; set; }

    [Range(0, int.MaxValue)]
    public int AnalysisQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int SlideQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int VideoQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int GameQuotaAmount { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AnalysisQuotaAmount == 0 && SlideQuotaAmount == 0 && VideoQuotaAmount == 0 && GameQuotaAmount == 0)
            yield return new ValidationResult(
                "Ít nhất 1 loại quota phải lớn hơn 0",
                new[] { nameof(AnalysisQuotaAmount) });
    }
}
