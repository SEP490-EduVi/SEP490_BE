using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class CreateSubjectRequestDto
{
    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(20)]
    public string SubjectCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "SubjectName is required")]
    [MaxLength(100)]
    public string SubjectName { get; set; } = string.Empty;
}
