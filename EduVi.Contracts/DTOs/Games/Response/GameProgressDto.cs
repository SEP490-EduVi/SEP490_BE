namespace EduVi.Contracts.DTOs.Games.Response;

public class GameProgressDto
{
    public Guid TaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Step { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string? Detail { get; set; }
    public object? Result { get; set; }
    public string? Error { get; set; }
}
