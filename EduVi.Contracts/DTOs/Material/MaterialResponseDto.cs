namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Response trả về thông tin material.
/// </summary>
public class MaterialResponseDto
{
    public string MaterialCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// image | video | audio | document | quiz | flashcard | lesson_plan
    /// </summary>
    public string Type { get; set; } = null!;

    public decimal? Price { get; set; }
    public string? PreviewUrl { get; set; }

    /// <summary>
    /// Resource URL — chỉ trả về nếu Teacher đã mua hoặc Expert sở hữu
    /// </summary>
    public string? ResourceUrl { get; set; }

    public string? SubjectCode { get; set; }
    public string? SubjectName { get; set; }
    public string? GradeCode { get; set; }
    public string? GradeName { get; set; }

    /// <summary>
    /// 0 = Pending, 1 = Approved, 2 = Rejected
    /// </summary>
    public int ApprovalStatus { get; set; }

    public string? ExpertCode { get; set; }
    public string? ExpertName { get; set; }

    public DateTime? CreatedAt { get; set; }
}
