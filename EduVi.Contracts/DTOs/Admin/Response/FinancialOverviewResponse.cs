namespace EduVi.Contracts.DTOs.Admin.Response;

/// <summary>
/// Tổng quan tài chính hệ thống (Dashboard)
/// </summary>
public class FinancialOverviewResponse
{
    // === Người dùng ===
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public int BannedUsers { get; set; }

    // === Ví ===
    public int TotalWallets { get; set; }

    /// <summary>
    /// Tổng số dư ví tất cả user (EduCoin đang lưu hành)
    /// </summary>
    public decimal TotalBalance { get; set; }

    // === Giao dịch ===
    /// <summary>
    /// Tổng số tiền nạp đã hoàn thành (VND quy đổi)
    /// </summary>
    public decimal TotalTopUpAmount { get; set; }
    public int TotalTopUpCount { get; set; }

    /// <summary>
    /// Tổng doanh thu mua gói (EduCoin)
    /// </summary>
    public decimal TotalSubscriptionRevenue { get; set; }
    public int TotalSubscriptionCount { get; set; }

    // === Đơn hàng ===
    public int TotalOrders { get; set; }
    public int CompletedOrders { get; set; }
}
