namespace EduVi.Contracts.DTOs.TextbookIngestion;

/// <summary>
/// DTO nhận từ RabbitMQ queue "textbook.ingestion.results" do Python worker gửi về.
/// Dùng cho cả ingest jobs lẫn delete jobs.
/// </summary>
public class TextbookIngestionProgressDto
{
    public string TaskId { get; set; } = string.Empty;
    public int DocumentId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Step { get; set; }
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public string? Error { get; set; }
    public object? Stats { get; set; }
}
