using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO cho việc Teacher lưu slide đã chỉnh sửa
/// </summary>
public class SaveEditedSlideRequestDto
{
    /// <summary>
    /// Toàn bộ JSON slide sau khi Teacher chỉnh sửa (cấu trúc cards)
    /// </summary>
    [Required(ErrorMessage = "SlideDocument không được để trống")]
    public required string SlideDocument { get; set; }
}
