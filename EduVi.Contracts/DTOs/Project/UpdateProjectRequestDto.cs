using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Project;

public class UpdateProjectRequestDto
{
    [MaxLength(100)]
    public string? ProjectCode { get; set; }

    [MaxLength(200)]
    public string? ProjectName { get; set; }

    public int? Status { get; set; }
}
