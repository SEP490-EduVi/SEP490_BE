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
}
