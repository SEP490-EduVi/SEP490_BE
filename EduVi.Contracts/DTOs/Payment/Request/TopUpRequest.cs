using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Payment.Request;

/// <summary>
/// Request nạp tiền EduCoin vào ví qua PayOS
/// </summary>
public class TopUpRequest
{
    /// <summary>
    /// Số tiền nạp (VND). Min 10,000 - Max 50,000,000
    /// </summary>
    [Required(ErrorMessage = "Amount is required")]
    [Range(10000, 50000000, ErrorMessage = "Amount must be between 10,000 and 50,000,000 VND")]
    public long Amount { get; set; }

    /// <summary>
    /// Mô tả giao dịch (tùy chọn)
    /// </summary>
    [MaxLength(200)]
    public string? Description { get; set; }

    /// <summary>
    /// URL redirect sau khi thanh toán thành công
    /// </summary>
    [Required(ErrorMessage = "ReturnUrl is required")]
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>
    /// URL redirect khi hủy thanh toán
    /// </summary>
    [Required(ErrorMessage = "CancelUrl is required")]
    public string CancelUrl { get; set; } = string.Empty;
}
