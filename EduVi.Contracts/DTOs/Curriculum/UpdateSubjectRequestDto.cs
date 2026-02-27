using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class UpdateSubjectRequestDto
{
    [MaxLength(20)]
    public string? SubjectCode { get; set; }

    [MaxLength(100)]
    public string? SubjectName { get; set; }
}
