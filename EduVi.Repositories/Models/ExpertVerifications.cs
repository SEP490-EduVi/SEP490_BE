#nullable disable
using System;

namespace EduVi.Repositories.Models;

public partial class ExpertVerifications
{
    public int VerificationId { get; set; }

    public string VerificationCode { get; set; }

    public int ExpertId { get; set; }

    public string FileUrl { get; set; }

    public string FileType { get; set; }

    public string Description { get; set; }

    /// <summary>0 = Pending, 1 = Approved, 2 = Rejected</summary>
    public int Status { get; set; }

    public string RejectionReason { get; set; }

    public DateTime UploadedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public int? ReviewedByStaffId { get; set; }

    public virtual Experts Expert { get; set; }
    public virtual Staffs ReviewedByStaff { get; set; }
}
