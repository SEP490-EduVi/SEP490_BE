using System.Text.Json.Serialization;
using EduVi.Contracts.DTOs.Games.Response;

namespace EduVi.Contracts.DTOs.Games.Request;

public class GameGenerationRequestMessage
{
    [JsonPropertyName("taskId")]
    public string TaskId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("templateVersion")]
    public string? TemplateVersion { get; set; }

    [JsonPropertyName("templateJson")]
    public string? TemplateJson { get; set; }

    [JsonPropertyName("slideEditedDocumentUrl")]
    public string? SlideEditedDocumentUrl { get; set; }

    [JsonPropertyName("roundCount")]
    public int RoundCount { get; set; }

    [JsonPropertyName("createdAt")]
    public string CreatedAt { get; set; } = string.Empty;
}
