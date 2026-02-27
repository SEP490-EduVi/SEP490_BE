namespace EduVi.Contracts.DTOs.Admin.Response;

/// <summary>
/// Danh sách ví tất cả user cho Admin
/// </summary>
public class AdminWalletResponse
{
    public int WalletId { get; set; }
    public int? UserId { get; set; }
    public string? Username { get; set; }
    public string? FullName { get; set; }
    public string? Email { get; set; }
    public decimal Balance { get; set; }
    public DateTime? LastUpdated { get; set; }
}
