namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Response DTO sau khi upload InputDocument thành công
/// </summary>
public class InputDocumentResponseDto
{
    public string? DocumentCode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string? ProjectCode { get; set; }
    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
    public string? GradeCode { get; set; }
    public string? GradeName { get; set; }
    public string? LessonCode { get; set; }
    public string? LessonName { get; set; }
    public DateTime? UploadDate { get; set; }
}
