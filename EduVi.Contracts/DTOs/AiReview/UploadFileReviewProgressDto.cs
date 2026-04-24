namespace EduVi.Contracts.DTOs.AIReview;

/// <summary>
/// Kết quả AI worker trả về cho luồng review file upload.
/// </summary>
public class UploadFileReviewProgressDto
{
    public Guid TaskId { get; set; }
    public int ExpertId { get; set; }
    public string ReviewKind { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public UploadFileReviewDecisionDto? Result { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Quyết định của AI worker cho một file upload.
/// </summary>
public class UploadFileReviewDecisionDto
{
    public bool IsValid { get; set; }
    public string? RejectionReason { get; set; }
    public string? Summary { get; set; }
}
