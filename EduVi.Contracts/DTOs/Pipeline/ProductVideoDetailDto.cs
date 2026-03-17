using System.Text.Json;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Chi tiết một bản ghi video pipeline của product.
/// </summary>
public class ProductVideoDetailDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductVideoCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? SlideDocumentUrl { get; set; }
    public string? VideoUrl { get; set; }
    public double? Duration { get; set; }
    public JsonElement? Interactions { get; set; }
    public JsonElement? PausePoints { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
