namespace EduVi.Contracts.DTOs.Games.Response;

public class GameSummaryDto
{
    public string GameCode { get; set; } = string.Empty;
    public string ProductGameCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
