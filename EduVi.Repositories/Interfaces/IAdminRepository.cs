using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IAdminRepository
{
    // ============ User Management ============

    /// <summary>
    /// Danh sách user với bộ lọc, phân trang. Include Role + sub-entities.
    /// </summary>
    Task<(List<Users> Items, int TotalCount)> GetUsersAsync(
        int? roleId, int? status, string? search,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize);

    /// <summary>
    /// Lấy user theo ID kèm Role + sub-entities. Dùng cho Admin xem chi tiết.
    /// </summary>
    Task<Users?> GetUserByIdAsync(int userId);

    /// <summary>
    /// Cập nhật thông tin cơ bản user (FullName, Email, Phone, Avatar).
    /// </summary>
    Task UpdateUserAsync(Users user);

    /// <summary>
    /// Cập nhật trạng thái user (Ban/Unban). 0 = Banned, 1 = Active.
    /// </summary>
    Task<bool> UpdateUserStatusAsync(int userId, int status);

    /// <summary>
    /// Thay đổi role của user.
    /// </summary>
    Task<bool> ChangeUserRoleAsync(int userId, int newRoleId);

    /// <summary>
    /// Xóa user (hard delete). Cẩn thận: chỉ dùng khi thật sự cần.
    /// </summary>
    Task<bool> DeleteUserAsync(int userId);

    /// <summary>
    /// Lấy danh sách tất cả roles.
    /// </summary>
    Task<List<Roles>> GetAllRolesAsync();

    /// <summary>
    /// Kiểm tra role tồn tại.
    /// </summary>
    Task<bool> RoleExistsAsync(int roleId);

    // ============ Financial ============

    /// <summary>
    /// Lấy tất cả ví kèm thông tin user.
    /// </summary>
    Task<(List<Wallets> Items, int TotalCount)> GetAllWalletsAsync(int page, int pageSize);

    /// <summary>
    /// Tổng số dư tất cả ví.
    /// </summary>
    Task<decimal> GetTotalWalletBalanceAsync();

    /// <summary>
    /// Đếm user theo trạng thái.
    /// </summary>
    Task<(int Total, int Active, int Banned)> GetUserCountsAsync();

    /// <summary>
    /// Giao dịch với bộ lọc cho Admin (tất cả user).
    /// </summary>
    Task<(List<WalletTransactions> Items, int TotalCount)> GetAllTransactionsAsync(
        int? userId, string? transactionType, int? status,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize);

    /// <summary>
    /// Thống kê tổng tiền nạp (TopUp completed).
    /// </summary>
    Task<(decimal TotalAmount, int Count)> GetTopUpStatsAsync();

    /// <summary>
    /// Thống kê tổng doanh thu mua gói (BuySubscription completed).
    /// </summary>
    Task<(decimal TotalAmount, int Count)> GetSubscriptionStatsAsync();

    // ============ Orders ============

    /// <summary>
    /// Danh sách đơn hàng với bộ lọc.
    /// </summary>
    Task<(List<Orders> Items, int TotalCount)> GetAllOrdersAsync(
        int? teacherId, int? status, string? paymentMethod,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize);

    /// <summary>
    /// Tổng số đơn + đơn completed.
    /// </summary>
    Task<(int Total, int Completed)> GetOrderCountsAsync();

    // ============ Subscription Plans ============

    Task<List<SubscriptionPlans>> GetAllPlansAsync();
    Task<SubscriptionPlans?> GetPlanByIdAsync(int planId);
    Task<SubscriptionPlans> CreatePlanAsync(SubscriptionPlans plan);
    Task UpdatePlanAsync(SubscriptionPlans plan);
    Task<bool> DeletePlanAsync(int planId);
}
