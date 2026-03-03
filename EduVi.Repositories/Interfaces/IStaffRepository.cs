using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IStaffRepository
{
    // ── Kiểm duyệt hồ sơ Expert ───────────────────────────────────────────────

    /// <summary>
    /// Lấy tất cả hồ sơ đang chờ duyệt, kèm navigation Expert → Users.
    /// </summary>
    Task<List<ExpertVerifications>> GetPendingVerificationsAsync();

    /// <summary>
    /// Lấy chi tiết 1 hồ sơ theo code, kèm navigation Expert → Users.
    /// </summary>
    Task<ExpertVerifications?> GetVerificationByCodeAsync(string verificationCode);

    /// <summary>
    /// Cập nhật bản ghi verification sau khi Staff approve hoặc reject.
    /// </summary>
    void UpdateVerification(ExpertVerifications verification);

    // ── Cập nhật trạng thái Expert ────────────────────────────────────────────

    /// <summary>
    /// Lấy Expert record kèm Users navigation để đọc/cập nhật IsVerified.
    /// </summary>
    Task<Experts?> GetExpertByIdAsync(int expertId);

    /// <summary>
    /// Cập nhật Expert record (set IsVerified sau approve/reject).
    /// </summary>
    void UpdateExpert(Experts expert);

    /// <summary>
    /// Kiểm tra Expert có ít nhất 1 hồ sơ approved (khác code đang xét) không.
    /// Dùng khi reject để quyết định có set IsVerified = false hay không.
    /// </summary>
    Task<bool> HasOtherApprovedVerificationAsync(int expertId, string excludeVerificationCode);
}
