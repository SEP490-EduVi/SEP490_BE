using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.ProductMaterial;

public class CreateProductMaterialRequestDto
{
    [JsonPropertyName("sourceType")]
    [Required]
    public string SourceType { get; set; } = string.Empty;

    [JsonPropertyName("materialCode")]
    public string? MaterialCode { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resourceUrl")]
    public string? ResourceUrl { get; set; }

    [JsonPropertyName("previewUrl")]
    public string? PreviewUrl { get; set; }
}

public class UpdateProductMaterialRequestDto
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("resourceUrl")]
    public string? ResourceUrl { get; set; }

    [JsonPropertyName("previewUrl")]
    public string? PreviewUrl { get; set; }
}
