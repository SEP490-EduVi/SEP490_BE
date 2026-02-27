namespace EduVi.Contracts.DTOs.Project;

public class ProjectResponseDto
{
    public string ProjectCode { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public int? Status { get; set; }
}
