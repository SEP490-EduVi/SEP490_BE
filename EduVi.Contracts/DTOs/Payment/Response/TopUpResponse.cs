namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Response chứa link thanh toán PayOS sau khi tạo lệnh nạp tiền
/// </summary>
public class TopUpResponse
{
    /// <summary>
    /// Mã giao dịch nội bộ (orderCode)
    /// </summary>
    public long OrderCode { get; set; }

    /// <summary>
    /// URL chuyển hướng đến trang thanh toán PayOS
    /// </summary>
    public string CheckoutUrl { get; set; } = string.Empty;

    /// <summary>
    /// Số tiền nạp (VND)
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// Trạng thái giao dịch
    /// </summary>
    public string Status { get; set; } = "PENDING";
}
