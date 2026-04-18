namespace EduVi.Contracts.DTOs.Games.Response;

public class GameTaskResponseDto
{
    public Guid TaskId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public string ProductGameCode { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductGameName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
