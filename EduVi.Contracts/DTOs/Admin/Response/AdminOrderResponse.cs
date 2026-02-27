namespace EduVi.Contracts.DTOs.Admin.Response;

/// <summary>
/// Thông tin đơn hàng cho Admin
/// </summary>
public class AdminOrderResponse
{
    public int OrderId { get; set; }
    public int? TeacherId { get; set; }
    public string? TeacherName { get; set; }
    public decimal? TotalAmount { get; set; }
    public DateTime? OrderDate { get; set; }
    public int? Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? PaymentMethod { get; set; }
}
