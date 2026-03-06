using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Material;

/// <summary>
/// Staff phê duyệt hoặc từ chối material.
/// </summary>
public class ReviewMaterialRequestDto
{
    /// <summary>
    /// true = approve, false = reject
    /// </summary>
    [Required]
    public bool Approved { get; set; }

    /// <summary>
    /// Lý do từ chối (bắt buộc khi reject)
    /// </summary>
    public string? RejectionReason { get; set; }
}
