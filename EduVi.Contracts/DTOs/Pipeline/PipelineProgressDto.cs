namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// DTO tiến trình pipeline — Python worker gửi về qua RabbitMQ, 
/// .NET push xuống client qua SignalR
/// </summary>
public class PipelineProgressDto
{
    public Guid TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int ProductId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}
