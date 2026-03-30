namespace EduVi.Contracts.DTOs.Project;

public class ProjectGroupedBySubjectResponseDto
{
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<ProjectGroupedByGradeResponseDto> Grades { get; set; } = new();
}