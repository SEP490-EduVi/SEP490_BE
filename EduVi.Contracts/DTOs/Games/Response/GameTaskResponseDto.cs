namespace EduVi.Contracts.DTOs.Games.Response;

public class GameTaskResponseDto
{
    public Guid TaskId { get; set; }
    public string TemplateId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
