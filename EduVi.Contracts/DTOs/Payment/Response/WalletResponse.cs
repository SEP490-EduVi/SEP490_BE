namespace EduVi.Contracts.DTOs.Payment.Response;

/// <summary>
/// Thông tin ví EduCoin của người dùng
/// </summary>
public class WalletResponse
{
    public int WalletId { get; set; }
    public int UserId { get; set; }

    /// <summary>
    /// Số dư EduCoin (1 EduCoin = 1 VND)
    /// </summary>
    public decimal Balance { get; set; }

    public DateTime? LastUpdated { get; set; }
}
