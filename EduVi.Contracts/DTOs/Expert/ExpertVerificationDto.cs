namespace EduVi.Contracts.DTOs.Expert;

/// <summary>
/// Thông tin hồ sơ verification trả về cho Expert (không chứa Signed URL).
/// </summary>
public class ExpertVerificationDto
{
    public string VerificationCode { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? Description { get; set; }

    /// <summary>
    /// pending | approved | rejected
    /// </summary>
    public string Status { get; set; } = null!;

    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Thông tin hồ sơ verification dành cho Staff — có Signed URL để xem file.
/// </summary>
public class ExpertVerificationStaffDto
{
    public string VerificationCode { get; set; } = null!;
    public int ExpertId { get; set; }
    public string ExpertName { get; set; } = null!;
    public string ExpertEmail { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? Description { get; set; }
    public string Status { get; set; } = null!;
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// GCS Signed URL có hiệu lực 15 phút để Staff xem file. Không trả về GCS path thô.
    /// </summary>
    public string SignedUrl { get; set; } = null!;
}

/// <summary>
/// Request body khi Staff approve hoặc reject hồ sơ.
/// </summary>
public class ReviewVerificationRequestDto
{
    /// <summary>
    /// true = Approve, false = Reject
    /// </summary>
    public bool Approved { get; set; }

    /// <summary>
    /// Bắt buộc khi Approved = false.
    /// </summary>
    public string? RejectionReason { get; set; }
}
