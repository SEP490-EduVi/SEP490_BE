using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.Games.Request;

public class GameConfigRequest
{
    [JsonPropertyName("templateId")]
    [Required]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("slideEditedDocumentUrl")]
    [Required]
    public string SlideEditedDocumentUrl { get; set; } = string.Empty;

    [JsonPropertyName("roundCount")]
    public int? RoundCount { get; set; }
}
