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
    /// 0 = Pending, 1 = Approved, 2 = Rejected
    /// </summary>
    public int Status { get; set; }

    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

/// <summary>
/// Thông tin hồ sơ verification dành cho Staff — có URL proxy nội bộ để xem file.
/// </summary>
public class ExpertVerificationStaffDto
{
    public string VerificationCode { get; set; } = null!;
    public int ExpertId { get; set; }
    public string ExpertName { get; set; } = null!;
    public string ExpertEmail { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string? Description { get; set; }
    /// <summary>0 = Pending, 1 = Approved, 2 = Rejected</summary>
    public int Status { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime UploadedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// URL proxy nội bộ để Staff xem file qua backend.
    /// </summary>
    public string FileUrl { get; set; } = null!;
}

/// <summary>
/// Payload file verification do service trả về để controller stream xuống client.
/// </summary>
public class ExpertVerificationFileDto
{
    public byte[] FileBytes { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "application/octet-stream";
    public string FileName { get; set; } = "verification-file";
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
