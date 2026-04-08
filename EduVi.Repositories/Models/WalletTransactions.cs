#nullable disable
using System;

namespace EduVi.Repositories.Models;

/// <summary>
/// Lịch sử giao dịch ví - Lưu mọi thay đổi số dư.
/// Dùng OrderCode (unique) để chống cộng tiền 2 lần (idempotency).
/// </summary>
public partial class WalletTransactions
{
    public int TransactionId { get; set; }

    public int? WalletId { get; set; }

    public long OrderCode { get; set; }

    public string TransactionType { get; set; }

    public decimal? Amount { get; set; }

    public decimal? BalanceBefore { get; set; }

    public decimal? BalanceAfter { get; set; }

    /// <summary>0 = Pending, 1 = Completed, 2 = Failed, 3 = Cancelled — xem PaymentConstants.Status</summary>
    public int? Status { get; set; }

    public string Description { get; set; }

    public int? PlanId { get; set; }

    public int? MaterialId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Wallets Wallet { get; set; }
    public virtual SubscriptionPlans Plan { get; set; }
    public virtual Materials Material { get; set; }
}
