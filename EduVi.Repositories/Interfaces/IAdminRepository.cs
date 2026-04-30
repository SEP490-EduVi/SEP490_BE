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
    /// Lấy user theo UserCode kèm Role + sub-entities. Dùng cho Admin API.
    /// </summary>
    Task<Users?> GetUserByCodeAsync(string userCode);

    /// <summary>
    /// Cập nhật thông tin cơ bản user (FullName, Phone, Avatar).
    /// </summary>
    Task UpdateUserAsync(Users user);

    /// <summary>
    /// Cập nhật trạng thái user (Ban/Unban). 0 = Banned, 1 = Active.
    /// </summary>
    Task<bool> UpdateUserStatusAsync(int userId, int status);

    /// <summary>
    /// Xóa toàn bộ quyền sở hữu học liệu đã mua của Teacher khi bị ban.
    /// </summary>
    Task<int> RemoveTeacherOwnedMaterialsAsync(int teacherId);

    /// <summary>
    /// Ẩn học liệu đã duyệt của Expert khỏi marketplace khi bị ban.
    /// Chỉ cập nhật các học liệu đang Approved (1) sang Banned (3).
    /// </summary>
    Task<int> HideApprovedMaterialsByExpertAsync(int expertId, string reason);

    /// <summary>
    /// Mở lại học liệu từng bị ẩn do ban Expert khi unban.
    /// Chỉ khôi phục các học liệu Banned (3) có đúng lý do bị ẩn do ban.
    /// </summary>
    Task<int> RestoreMaterialsHiddenByExpertBanAsync(int expertId, string reason);

    /// <summary>
    /// Xóa user (hard delete). Cẩn thận: chỉ dùng khi thật sự cần.
    /// </summary>
    Task<bool> DeleteUserAsync(int userId);

    /// <summary>
    /// Lấy danh sách tất cả roles.
    /// </summary>
    Task<List<Roles>> GetAllRolesAsync();

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

    /// <summary>
    /// Doanh thu theo từng học liệu đã bán, có lọc theo thời gian và metadata.
    /// </summary>
    Task<(List<MaterialSalesAnalyticsRow> Items, int TotalCount)> GetMaterialSalesAnalyticsAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode,
        int page,
        int pageSize);

    /// <summary>
    /// Doanh thu theo từng Expert, có lọc theo thời gian và metadata.
    /// </summary>
    Task<(List<ExpertSalesAnalyticsRow> Items, int TotalCount)> GetExpertSalesAnalyticsAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode,
        int page,
        int pageSize);

    /// <summary>
    /// Dữ liệu tổng hợp để dự báo doanh thu theo kỳ hiện tại và kỳ trước liền kề.
    /// </summary>
    Task<RevenueForecastAnalyticsRow> GetRevenueForecastAnalyticsAsync(
        DateTime currentFromDate,
        DateTime currentToDate,
        DateTime previousFromDate,
        DateTime previousToDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode);

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

    Task<List<QuotaPlans>> GetAllPlansAsync();
    Task<QuotaPlans?> GetPlanByIdAsync(int planId);
    Task<QuotaPlans> CreatePlanAsync(QuotaPlans plan);
    Task UpdatePlanAsync(QuotaPlans plan);
    Task<bool> DeletePlanAsync(int planId);

    // ============ Materials (Admin CRUD) ============

    /// <summary>
    /// Danh sách học liệu cho Admin với bộ lọc, phân trang.
    /// </summary>
    Task<(List<Materials> Items, int TotalCount)> GetMaterialsForAdminAsync(
        int? approvalStatus, string? type, string? subjectCode, string? gradeCode,
        string? expertCode, string? search, int page, int pageSize);

    /// <summary>
    /// Lấy chi tiết học liệu theo MaterialCode (kèm Expert, Subject, Grade).
    /// </summary>
    Task<Materials?> GetMaterialByCodeWithDetailsAsync(string materialCode);

    /// <summary>
    /// Kiểm tra MaterialCode đã tồn tại hay chưa.
    /// </summary>
    Task<bool> MaterialCodeExistsAsync(string materialCode);

    /// <summary>
    /// Lấy Expert theo ExpertCode.
    /// </summary>
    Task<Experts?> GetExpertByCodeAsync(string expertCode);

    /// <summary>
    /// Lấy Subject theo SubjectCode.
    /// </summary>
    Task<Subjects?> GetSubjectByCodeAsync(string subjectCode);

    /// <summary>
    /// Lấy Grade theo GradeCode.
    /// </summary>
    Task<Grades?> GetGradeByCodeAsync(string gradeCode);

    /// <summary>
    /// Tạo học liệu mới.
    /// </summary>
    Task<Materials> CreateMaterialAsync(Materials material);

    /// <summary>
    /// Cập nhật học liệu.
    /// </summary>
    void UpdateMaterial(Materials material);

    /// <summary>
    /// Xóa học liệu.
    /// </summary>
    void DeleteMaterial(Materials material);

    /// <summary>
    /// Kiểm tra học liệu đã phát sinh phụ thuộc (mua hàng/giao dịch/gắn product) hay chưa.
    /// </summary>
    Task<bool> HasMaterialDependenciesAsync(int materialId);

    // ============ Platform Wallet ============

    /// <summary>
    /// Lấy ví của Admin (platform) để cộng phần doanh thu 30% từ material.
    /// Trả về ví của Admin đầu tiên (RoleId = 1) có ví hợp lệ.
    /// </summary>
    Task<Wallets?> GetAdminWalletAsync();

    /// <summary>
    /// Cập nhật số dư ví Admin sau khi nhận doanh thu.
    /// </summary>
    void UpdateWallet(Wallets wallet);

    /// <summary>
    /// Tạo giao dịch ví cho Admin (MATERIAL_PLATFORM_FEE).
    /// </summary>
    Task CreateWalletTransactionAsync(WalletTransactions transaction);
}
