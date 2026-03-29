namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Thông tin tóm tắt của Product — dùng cho danh sách (không chứa JSON nặng)
/// </summary>
public class ProductSummaryDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? DocumentCode { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    // Các mốc thời gian để hiển thị tiến trình
    public DateTime? EvaluatedAt { get; set; }
    public DateTime? SlideGeneratedAt { get; set; }
    public DateTime? SlideEditedAt { get; set; }

    // Flags giúp frontend biết bước nào đã hoàn thành
    public bool HasEvaluation { get; set; }
    public bool HasSlide { get; set; }
    public bool HasEditedSlide { get; set; }
}
