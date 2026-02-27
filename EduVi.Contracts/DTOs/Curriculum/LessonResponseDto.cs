namespace EduVi.Contracts.DTOs.Curriculum;

public class LessonResponseDto
{
    public string LessonCode { get; set; } = string.Empty;
    public string LessonName { get; set; } = string.Empty;
    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
}
