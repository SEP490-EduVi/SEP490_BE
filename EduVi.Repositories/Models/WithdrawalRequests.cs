#nullable disable

namespace EduVi.Repositories.Models;

/// <summary>
/// Yêu cầu rút tiền từ ví Expert.
/// Lifecycle: PENDING → CONFIRMED (tiền bị freeze) → SUCCESS (tiền trừ thật) hoặc REJECTED (tiền unlock).
/// </summary>
public class WithdrawalRequests
{
    public int WithdrawalId { get; set; }

    public int UserId { get; set; }

    /// <summary>Số tiền muốn rút (>= 200,000 VND)</summary>
    public decimal Amount { get; set; }

    /// <summary>Số tiền bị freeze trong ví, bằng Amount khi tạo request</summary>
    public decimal LockedAmount { get; set; }

    public string BankAccountNumber { get; set; }

    public string BankName { get; set; }

    public string AccountHolderName { get; set; }

    /// <summary>PENDING | CONFIRMED | SUCCESS | REJECTED</summary>
    public string Status { get; set; }

    /// <summary>Ghi chú của Admin khi duyệt/từ chối</summary>
    public string AdminNote { get; set; }

    public int? ProcessedByAdminId { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Users User { get; set; }
    public virtual Users ProcessedByAdmin { get; set; }
}
