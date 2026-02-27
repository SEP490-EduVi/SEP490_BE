using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Curriculum;

public class UpdateGradeRequestDto
{
    [MaxLength(50)]
    public string? GradeCode { get; set; }

    [MaxLength(50)]
    public string? GradeName { get; set; }
}
