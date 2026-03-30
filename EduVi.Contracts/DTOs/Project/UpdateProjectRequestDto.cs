using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Project;

public class UpdateProjectRequestDto
{
    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    public string? ProjectName { get; set; }

    [MaxLength(20)]
    public string? SubjectCode { get; set; }

    [MaxLength(50)]
    public string? GradeCode { get; set; }

    public int? Status { get; set; }
}
