using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.Games.Request;

public class GameConfigRequest
{
    [JsonPropertyName("productCode")]
    [Required]
    public string ProductCode { get; set; } = string.Empty;

    [JsonPropertyName("productGameName")]
    [Required]
    [MaxLength(200)]
    public string ProductGameName { get; set; } = string.Empty;

    [JsonPropertyName("templateId")]
    [Required]
    public string TemplateId { get; set; } = string.Empty;

    [JsonPropertyName("slideEditedDocumentUrl")]
    [Required]
    public string SlideEditedDocumentUrl { get; set; } = string.Empty;

    [JsonPropertyName("roundCount")]
    public int? RoundCount { get; set; }
}
