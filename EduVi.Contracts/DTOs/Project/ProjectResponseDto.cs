using System;

namespace EduVi.Contracts.DTOs.Project;

public class ProjectResponseDto
{
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public int? Status { get; set; }
    public DateTime? CreatedAt { get; set; }
}
