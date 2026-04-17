using System.Text.Json;

namespace EduVi.Contracts.DTOs.Template;

public class TemplateResponseDto
{
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Category { get; set; } = string.Empty;
    public JsonElement Skeleton { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
