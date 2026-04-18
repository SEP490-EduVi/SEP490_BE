namespace EduVi.Contracts.DTOs.ProductMaterial;

public class ProductMaterialResponseDto
{
    public string ProductMaterialCode { get; set; } = string.Empty;

    public string ProductCode { get; set; } = string.Empty;

    public string SourceType { get; set; } = string.Empty;

    public string? MaterialCode { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Type { get; set; } = string.Empty;

    public string ResourceUrl { get; set; } = string.Empty;

    public string? PreviewUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
