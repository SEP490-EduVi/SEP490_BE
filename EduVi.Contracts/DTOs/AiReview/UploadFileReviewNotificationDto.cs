namespace EduVi.Contracts.DTOs.AIReview;

/// <summary>
/// Payload SignalR dùng để báo cho Expert/Staff về kết quả review file.
/// </summary>
public class UploadFileReviewNotificationDto
{
    public Guid TaskId { get; set; }
    public int ExpertId { get; set; }
    public string? ExpertName { get; set; }
    public string ReviewKind { get; set; } = string.Empty;
    public string EntityCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime OccurredAt { get; set; }
}
