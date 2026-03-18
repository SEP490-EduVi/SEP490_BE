namespace EduVi.Contracts.DTOs.CurriculumIngestion;

/// <summary>
/// DTO nhận từ RabbitMQ queue "curriculum.ingestion.results" do Python worker gửi về.
/// </summary>
public class CurriculumIngestionProgressDto
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
