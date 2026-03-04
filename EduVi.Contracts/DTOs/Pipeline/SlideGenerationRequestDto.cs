using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho việc tạo slide presentation từ evaluation result
/// </summary>
public class SlideGenerationRequestDto
{
    /// <summary>
    /// Code của Product đã được evaluate (có EvaluationResult)
    /// </summary>
    [Required(ErrorMessage = "ProductCode is required")]
    [MaxLength(100)]
    public string ProductCode { get; set; } = string.Empty;

    /// <summary>
    /// Phạm vi số lượng slide: "short" (5-8), "medium" (10-15), "detailed" (15-20)
    /// </summary>
    [Required(ErrorMessage = "SlideRange is required")]
    [RegularExpression("^(short|medium|detailed)$", ErrorMessage = "SlideRange must be 'short', 'medium', or 'detailed'")]
    public string SlideRange { get; set; } = "medium";
}
