using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin tạo học liệu mới.
/// </summary>
public class CreateAdminMaterialRequest : IValidatableObject
{
    [MaxLength(100)]
    public string? ExpertCode { get; set; }

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty;

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    [Required]
    [MaxLength(500)]
    public string ResourceUrl { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? PreviewUrl { get; set; }

    [MaxLength(50)]
    public string? SubjectCode { get; set; }

    [MaxLength(50)]
    public string? GradeCode { get; set; }

    /// <summary>
    /// 0 = Pending, 1 = Approved, 2 = Rejected, 3 = Banned.
    /// </summary>
    [Range(0, 3)]
    public int? ApprovalStatus { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ApprovalStatus is 2 or 3 && string.IsNullOrWhiteSpace(RejectionReason))
        {
            yield return new ValidationResult(
                "Phải cung cấp lý do khi tạo học liệu ở trạng thái Rejected hoặc Banned",
                new[] { nameof(RejectionReason) });
        }
    }
}
