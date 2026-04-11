using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Admin.Request;

/// <summary>
/// Admin cập nhật gói cước (chỉ sửa tên, mô tả, trạng thái).
/// Giá và quyền lợi → cần tính năng Versioning (sẽ bổ sung sau).
/// </summary>
public class UpdatePlanRequest
{
    [MaxLength(100)]
    public string? PlanName { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(0, double.MaxValue)]
    public decimal? Price { get; set; }

    [Range(0, int.MaxValue)]
    public int? AnalysisQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? SlideQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? VideoQuotaAmount { get; set; }

    [Range(0, int.MaxValue)]
    public int? GameQuotaAmount { get; set; }

    /// <summary>
    /// Bật/tắt gói (true = đang bán, false = ẩn)
    /// </summary>
    public bool? IsActive { get; set; }
}
