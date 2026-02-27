namespace EduVi.Contracts.DTOs.Curriculum;

public class SubjectResponseDto
{
    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public int LessonCount { get; set; }
}
