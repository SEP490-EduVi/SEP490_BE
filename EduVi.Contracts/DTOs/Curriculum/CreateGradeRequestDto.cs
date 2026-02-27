using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class CreateGradeRequestDto
{
    [Required(ErrorMessage = "GradeCode is required")]
    [MaxLength(50)]
    public string GradeCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "GradeName is required")]
    [MaxLength(50)]
    public string GradeName { get; set; } = string.Empty;
}
