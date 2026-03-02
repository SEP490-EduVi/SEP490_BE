using EduVi.Contracts.DTOs.Admin.Request;
using EduVi.Contracts.DTOs.Admin.Response;

namespace EduVi.Services.Admin;

public interface IAdminService
{
    // ============ User Management ============

    /// <summary>
    /// Danh sách user với bộ lọc, phân trang
    /// </summary>
    Task<PagedResponse<AdminUserResponse>> GetUsersAsync(UserFilterRequest filter);

    /// <summary>
    /// Xem chi tiết 1 user
    /// </summary>
    Task<AdminUserResponse> GetUserByCodeAsync(string userCode);

    /// <summary>
    /// Cập nhật thông tin cơ bản user
    /// </summary>
    Task<AdminUserResponse> UpdateUserAsync(string userCode, UpdateUserRequest request);

    /// <summary>
    /// Ban user (Status=0) + Revoke Token ngay lập tức
    /// </summary>
    Task<bool> BanUserAsync(string userCode);

    /// <summary>
    /// Unban user (Status=1)
    /// </summary>
    Task<bool> UnbanUserAsync(string userCode);

    /// <summary>
    /// Thay đổi role + Revoke Token (user phải login lại với quyền mới)
    /// </summary>
    Task<bool> ChangeUserRoleAsync(string userCode, ChangeUserRoleRequest request);

    /// <summary>
    /// Xóa user (hard delete)
    /// </summary>
    Task<bool> DeleteUserAsync(string userCode);

    /// <summary>
    /// Lấy danh sách tất cả roles
    /// </summary>
    Task<List<RoleResponse>> GetAllRolesAsync();

    // ============ Financial ============

    /// <summary>
    /// Dashboard tài chính tổng quan
    /// </summary>
    Task<FinancialOverviewResponse> GetFinancialOverviewAsync();

    /// <summary>
    /// Danh sách ví tất cả user
    /// </summary>
    Task<PagedResponse<AdminWalletResponse>> GetAllWalletsAsync(int page, int pageSize);

    /// <summary>
    /// Danh sách giao dịch toàn hệ thống
    /// </summary>
    Task<PagedResponse<AdminTransactionResponse>> GetAllTransactionsAsync(TransactionFilterRequest filter);

    /// <summary>
    /// Danh sách đơn hàng
    /// </summary>
    Task<PagedResponse<AdminOrderResponse>> GetAllOrdersAsync(OrderFilterRequest filter);

    // ============ Subscription Plans ============

    /// <summary>
    /// Tất cả gói (bao gồm inactive)
    /// </summary>
    Task<List<PlanResponse>> GetAllPlansAsync();

    /// <summary>
    /// Chi tiết 1 gói
    /// </summary>
    Task<PlanResponse> GetPlanByIdAsync(int planId);

    /// <summary>
    /// Tạo gói mới
    /// </summary>
    Task<PlanResponse> CreatePlanAsync(CreatePlanRequest request);

    /// <summary>
    /// Cập nhật gói
    /// </summary>
    Task<PlanResponse> UpdatePlanAsync(int planId, UpdatePlanRequest request);

    /// <summary>
    /// Soft delete gói (IsActive = false)
    /// </summary>
    Task<bool> DeletePlanAsync(int planId);
}

// ============ Sub-DTOs cho Service ============

public class RoleResponse
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class PlanResponse
{
    public int PlanId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int DurationDays { get; set; }
    public int QuotaAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
