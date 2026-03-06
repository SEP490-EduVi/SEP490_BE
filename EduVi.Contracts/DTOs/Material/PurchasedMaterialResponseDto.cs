namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Thông tin material đã mua của Teacher.
/// </summary>
public class PurchasedMaterialResponseDto
{
    public string MaterialCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string Type { get; set; } = null!;
    public decimal? Price { get; set; }
    public string? ResourceUrl { get; set; }
    public string? PreviewUrl { get; set; }
    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
    public string? GradeCode { get; set; }
    public string? GradeName { get; set; }
    public string? ExpertCode { get; set; }
    public string? ExpertName { get; set; }
    public DateTime PurchasedDate { get; set; }
}
