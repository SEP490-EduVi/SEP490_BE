using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho việc Teacher lưu slide đã chỉnh sửa.
/// </summary>
public class SaveEditedSlideRequestDto
{
    /// <summary>
    /// URL file JSON slide đã upload sẵn lên GCS bởi FE.
    /// </summary>
    [Required(ErrorMessage = "SlideEditedDocumentUrl không được để trống")]
    public required string SlideEditedDocumentUrl { get; set; }

    /// <summary>
    /// Danh sách MaterialCode của các material được chèn vào slide.
    /// BE sẽ validate Teacher đã mua từng material này chưa.
    /// Null hoặc rỗng nếu slide không dùng material nào.
    /// </summary>
    public List<string>? UsedMaterialCodes { get; set; }
}
