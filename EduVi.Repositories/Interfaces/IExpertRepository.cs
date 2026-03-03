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
}
