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

    /// <summary>
    /// ID ví liên quan
    /// </summary>
    public int? WalletId { get; set; }

    /// <summary>
    /// Mã đơn hàng PayOS hoặc mã nội bộ (unique, dùng cho idempotency)
    /// </summary>
    public long OrderCode { get; set; }

    /// <summary>
    /// TOP_UP | BUY_SUBSCRIPTION
    /// </summary>
    public string TransactionType { get; set; }

    /// <summary>
    /// Số tiền giao dịch (dương = nạp, âm = trừ)
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Số dư trước giao dịch
    /// </summary>
    public decimal? BalanceBefore { get; set; }

    /// <summary>
    /// Số dư sau giao dịch
    /// </summary>
    public decimal? BalanceAfter { get; set; }

    /// <summary>
    /// 0 = PENDING, 1 = COMPLETED, 2 = FAILED, 3 = CANCELLED
    /// </summary>
    public int? Status { get; set; }

    /// <summary>
    /// Mô tả / ghi chú giao dịch
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// ID gói subscription (nếu là BUY_SUBSCRIPTION)
    /// </summary>
    public int? PlanId { get; set; }

    public DateTime? CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public virtual Wallets Wallet { get; set; }
    public virtual SubscriptionPlans Plan { get; set; }
}
