using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Request DTO tạo video từ SlideEditedDocument (GCS URL).
/// </summary>
public class GenerateVideoRequestDto
{
    /// <summary>
    /// ProductCode cần tạo video.
    /// </summary>
    [Required(ErrorMessage = "ProductCode không được để trống")]
    public required string ProductCode { get; set; }

    /// <summary>
    /// URL file slide edited trên GCS. Nếu gửi vào sẽ ghi đè SlideEditedDocument hiện tại của product.
    /// </summary>
    [Required(ErrorMessage = "SlideEditedDocumentUrl không được để trống")]
    public required string SlideEditedDocumentUrl { get; set; }
}
