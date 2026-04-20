using EduVi.Contracts.DTOs.Admin.Request;
using EduVi.Contracts.DTOs.Admin.Response;
using EduVi.Contracts.DTOs.Material;

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
    /// Tạo user mới từ màn Admin
    /// </summary>
    Task<AdminUserResponse> CreateUserAsync(CreateUserRequest request);

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

    // ============ Materials (Admin CRUD) ============

    /// <summary>
    /// Danh sách học liệu với bộ lọc, phân trang.
    /// </summary>
    Task<PagedResponse<MaterialResponseDto>> GetMaterialsForAdminAsync(AdminMaterialFilterRequest filter);

    /// <summary>
    /// Chi tiết học liệu theo MaterialCode.
    /// </summary>
    Task<MaterialResponseDto> GetMaterialDetailForAdminAsync(string materialCode);

    /// <summary>
    /// Tạo học liệu mới bởi Admin.
    /// </summary>
    Task<MaterialResponseDto> CreateMaterialForAdminAsync(CreateAdminMaterialRequest request);

    /// <summary>
    /// Cập nhật học liệu bởi Admin.
    /// </summary>
    Task<MaterialResponseDto> UpdateMaterialForAdminAsync(string materialCode, UpdateAdminMaterialRequest request);

    /// <summary>
    /// Xóa học liệu bởi Admin.
    /// </summary>
    Task<bool> DeleteMaterialForAdminAsync(string materialCode);
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
    public int AnalysisQuotaAmount { get; set; }
    public int SlideQuotaAmount { get; set; }
    public int VideoQuotaAmount { get; set; }
    public int GameQuotaAmount { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}
