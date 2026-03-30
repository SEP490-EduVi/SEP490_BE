using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Expert gửi metadata của file đã upload lên GCS.
/// </summary>
public class UploadFileMaterialRequestDto
{
    [Required]
    public string ResourceUrl { get; set; } = null!;

    /// <summary>
    /// Thumbnail/preview URL trên GCS (optional — nên có cho video)
    /// </summary>
    public string? PreviewUrl { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>
    /// image | video
    /// </summary>
    [Required]
    public string Type { get; set; } = null!;

    /// <summary>
    /// Giá bán (VND). Nếu null hoặc 0 thì miễn phí.
    /// </summary>
    public decimal? Price { get; set; }

    public string? SubjectCode { get; set; }

    public string? GradeCode { get; set; }
}
