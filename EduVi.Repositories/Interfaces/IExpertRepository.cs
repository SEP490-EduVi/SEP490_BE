using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IExpertRepository
{
    // ── Verifications ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo bản ghi verification mới khi Expert upload hồ sơ.
    /// </summary>
    Task CreateVerificationAsync(ExpertVerifications verification);

    /// <summary>
    /// Lấy toàn bộ hồ sơ của một Expert (để Expert xem trạng thái nộp của mình).
    /// </summary>
    Task<List<ExpertVerifications>> GetVerificationsByExpertAsync(int expertId);

    /// <summary>
    /// Lấy hồ sơ theo code — Expert dùng khi xóa (kiểm tra quyền sở hữu, không cần navigation).
    /// </summary>
    Task<ExpertVerifications?> GetVerificationByCodeAsync(string verificationCode);

    /// <summary>
    /// Xóa hồ sơ chưa được duyệt (Expert muốn nộp lại).
    /// </summary>
    void DeleteVerification(ExpertVerifications verification);

    // ── Expert profile ─────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy Expert record để kiểm tra IsVerified trước khi cho phép upload hồ sơ mới.
    /// </summary>
    Task<Experts?> GetExpertByIdAsync(int expertId);

    /// <summary>
    /// Lấy Expert kèm Users navigation để hiển thị hoặc cập nhật profile.
    /// ExpertId = UserId (FK).
    /// </summary>
    Task<Experts?> GetProfileByUserIdAsync(int userId);

    // ── Materials ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Tạo material mới (ApprovalStatus = 0 pending).
    /// </summary>
    Task CreateMaterialAsync(Materials material);

    /// <summary>
    /// Lấy danh sách materials của Expert theo ExpertId, kèm Subject/Grade navigation.
    /// </summary>
    Task<List<Materials>> GetMaterialsByExpertIdAsync(int expertId);

    /// <summary>
    /// Lấy 1 material theo code — Expert dùng để sửa/xóa (kiểm tra ownership bằng ExpertId).
    /// </summary>
    Task<Materials?> GetMaterialByCodeAsync(string materialCode);

    /// <summary>
    /// Cập nhật material (chỉ được khi chưa approve).
    /// </summary>
    void UpdateMaterial(Materials material);

    /// <summary>
    /// Xóa material (chỉ được khi chưa approve).
    /// </summary>
    void DeleteMaterial(Materials material);

    /// <summary>
    /// Lấy Subject theo SubjectCode — resolve FK khi Expert tạo/sửa material.
    /// </summary>
    Task<Subjects?> GetSubjectByCodeAsync(string subjectCode);

    /// <summary>
    /// Lấy Grade theo GradeCode — resolve FK khi Expert tạo/sửa material.
    /// </summary>
    Task<Grades?> GetGradeByCodeAsync(string gradeCode);

    /// <summary>
    /// Đếm số materials đang chờ duyệt (ApprovalStatus = 0) của Expert — dùng để giới hạn upload.
    /// </summary>
    Task<int> CountPendingMaterialsAsync(int expertId);

    /// <summary>
    /// Lấy Wallet của Expert theo UserId — dùng để cộng tiền khi Teacher mua material.
    /// </summary>
    Task<Wallets?> GetWalletByUserIdAsync(int userId);

    /// <summary>
    /// Cập nhật số dư ví Expert sau khi nhận doanh thu.
    /// </summary>
    void UpdateWallet(Wallets wallet);

    /// <summary>
    /// Tạo giao dịch ví cho Expert (MATERIAL_REVENUE).
    /// </summary>
    Task CreateWalletTransactionAsync(WalletTransactions transaction);

    /// <summary>
    /// Doanh số theo material của Expert hiện tại.
    /// </summary>
    Task<List<MaterialSalesAnalyticsRow>> GetMaterialSalesAnalyticsByExpertAsync(
        int expertId,
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? materialCode);

    /// <summary>
    /// Dữ liệu tổng hợp để Expert tự xem forecast theo kỳ hiện tại và kỳ trước.
    /// </summary>
    Task<RevenueForecastAnalyticsRow> GetRevenueForecastAnalyticsByExpertAsync(
        int expertId,
        DateTime currentFromDate,
        DateTime currentToDate,
        DateTime previousFromDate,
        DateTime previousToDate,
        string? subjectCode,
        string? gradeCode,
        string? materialCode);
}
