namespace EduVi.Contracts.DTOs.Project;

public class ProjectGroupedByGradeResponseDto
{
    public string GradeCode { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public List<ProjectResponseDto> Projects { get; set; } = new();
}