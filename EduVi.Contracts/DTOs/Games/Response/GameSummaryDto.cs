namespace EduVi.Contracts.DTOs.Games.Response;

public class GameSummaryDto
{
    public string GameCode { get; set; } = string.Empty;
    public string TeacherGameCode { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string TemplateCode { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
