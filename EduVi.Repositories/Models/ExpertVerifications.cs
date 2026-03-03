#nullable disable
using System;

namespace EduVi.Repositories.Models;

public partial class ExpertVerifications
{
    public int VerificationId { get; set; }

    /// <summary>
    /// Unique business code. Format: vrf_{expertId}_{type}_{yyyyMMdd_HHmmss}
    /// </summary>
    public string VerificationCode { get; set; }

    public int ExpertId { get; set; }

    /// <summary>
    /// GCS path: gs://eduvi_folders/expert_verifications/{expertId}/{filename}
    /// </summary>
    public string FileUrl { get; set; }

    /// <summary>
    /// Loại tài liệu: degree | certificate | id_card | other
    /// </summary>
    public string FileType { get; set; }

    public string Description { get; set; }

    /// <summary>
    /// pending | approved | rejected
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Lý do từ chối. Chỉ có giá trị khi Status = "rejected".
    /// </summary>
    public string RejectionReason { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByStaffId { get; set; }

    // Navigation properties
    public virtual Experts Expert { get; set; }
    public virtual Staffs ReviewedByStaff { get; set; }
}
