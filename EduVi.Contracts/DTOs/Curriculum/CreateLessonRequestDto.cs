using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class CreateLessonRequestDto
{
    [Required(ErrorMessage = "LessonCode is required")]
    [MaxLength(50)]
    public string LessonCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "LessonName is required")]
    [MaxLength(100)]
    public string LessonName { get; set; } = string.Empty;

    [Required(ErrorMessage = "SubjectCode is required")]
    [MaxLength(20)]
    public string SubjectCode { get; set; } = string.Empty;
}
