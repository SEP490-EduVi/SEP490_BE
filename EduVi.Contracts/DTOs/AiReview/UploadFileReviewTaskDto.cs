namespace EduVi.Contracts.DTOs.AIReview;

/// <summary>
/// Payload .NET gửi sang AI worker để phân tích file upload của Expert.
/// </summary>
public class UploadFileReviewTaskDto
{
    public Guid TaskId { get; set; }
    public string ReviewKind { get; set; } = null!;
    public int ExpertId { get; set; }
    public string EntityCode { get; set; } = null!;
    public string FileUrl { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string ContentType { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? PreviewUrl { get; set; }
    public string? SubjectCode { get; set; }
    public string? GradeCode { get; set; }
}
