using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin cập nhật học liệu.
/// </summary>
public class UpdateAdminMaterialRequest : IValidatableObject
{
    [MaxLength(100)]
    public string? ExpertCode { get; set; }

    [MaxLength(200)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [MaxLength(50)]
    public string? Type { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    [MaxLength(500)]
    public string? ResourceUrl { get; set; }

    [MaxLength(500)]
    public string? PreviewUrl { get; set; }

    [MaxLength(50)]
    public string? SubjectCode { get; set; }

    [MaxLength(50)]
    public string? GradeCode { get; set; }

    /// <summary>
    /// 0 = Pending, 1 = Approved, 2 = Rejected.
    /// </summary>
    [Range(0, 2)]
    public int? ApprovalStatus { get; set; }

    [MaxLength(500)]
    public string? RejectionReason { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ApprovalStatus == 2 && string.IsNullOrWhiteSpace(RejectionReason))
        {
            yield return new ValidationResult(
                "Phải cung cấp lý do khi chuyển trạng thái Rejected",
                new[] { nameof(RejectionReason) });
        }
    }
}
