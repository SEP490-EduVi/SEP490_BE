namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Thông tin quota hiện tại của giáo viên.
/// </summary>
public class UserQuotaResponse
{
    public int TotalAnalysisQuota { get; set; }
    public int AvailableAnalysisQuota { get; set; }
    public int UsedAnalysisQuota { get; set; }

    public int TotalSlideQuota { get; set; }
    public int AvailableSlideQuota { get; set; }
    public int UsedSlideQuota { get; set; }

    public int TotalVideoQuota { get; set; }
    public int AvailableVideoQuota { get; set; }
    public int UsedVideoQuota { get; set; }

    public int TotalGameQuota { get; set; }
    public int AvailableGameQuota { get; set; }
    public int UsedGameQuota { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
