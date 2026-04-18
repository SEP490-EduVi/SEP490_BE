namespace EduVi.Contracts.DTOs.Games.Response;

public class GameTaskResponseDto
{
    public Guid TaskId { get; set; }
    public string GameCode { get; set; } = string.Empty;
    public string TeacherGameCode { get; set; } = string.Empty;
    public string GameName { get; set; } = string.Empty;
    public string TemplateId { get; set; } = string.Empty;
    public int RoundCount { get; set; }
    public string Status { get; set; } = string.Empty;
}
