using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Project;

public class CreateProjectRequestDto
{
    [Required(ErrorMessage = "ProjectCode is required")]
    [MaxLength(100)]
    public string ProjectCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "ProjectName is required")]
    [MaxLength(200)]
    public string ProjectName { get; set; } = string.Empty;

    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(20)]
    public string SubjectCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "GradeCode is required")]
    [MaxLength(50)]
    public string GradeCode { get; set; } = string.Empty;
}
