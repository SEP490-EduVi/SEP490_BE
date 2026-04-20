using System.Text.Json;

namespace EduVi.Contracts.DTOs.Games.Response;

public class GameResultJsonDto
{
    public string ProductGameCode { get; set; } = string.Empty;
    public JsonElement? ResultJson { get; set; }
}