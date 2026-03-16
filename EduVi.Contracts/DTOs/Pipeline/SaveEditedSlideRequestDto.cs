using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho việc Teacher lưu slide đã chỉnh sửa
/// </summary>
public class SaveEditedSlideRequestDto
{
    /// <summary>
    /// URL file JSON slide đã upload sẵn lên GCS bởi FE.
    /// </summary>
    [Required(ErrorMessage = "SlideEditedDocumentUrl không được để trống")]
    public required string SlideEditedDocumentUrl { get; set; }

    /// <summary>
    /// Danh sách materials được chèn vào slide kèm vị trí (CardId, BlockId).
    /// Null hoặc rỗng nếu slide không dùng material nào.
    /// </summary>
    public List<SlideUsedMaterialDto>? UsedMaterials { get; set; }
}

/// <summary>
/// Thông tin một material được Teacher chèn vào slide.
/// </summary>
public class SlideUsedMaterialDto
{
    /// <summary>
    /// MaterialCode của material đã mua (dùng Code thay vì ID — external API rule).
    /// </summary>
    [Required]
    public required string MaterialCode { get; set; }

    /// <summary>
    /// ID của Card trong slide JSON. e.g. "card-acf4c8be"
    /// </summary>
    [Required]
    public required string CardId { get; set; }

    /// <summary>
    /// ID của Block trong Card. e.g. "block-media-001"
    /// </summary>
    [Required]
    public required string BlockId { get; set; }
}
