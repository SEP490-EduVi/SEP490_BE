using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class UpdateLessonRequestDto
{
    [MaxLength(50)]
    public string? LessonCode { get; set; }

    [MaxLength(100)]
    public string? LessonName { get; set; }

    [MaxLength(20)]
    public string? SubjectCode { get; set; }
}
