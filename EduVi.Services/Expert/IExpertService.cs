using EduVi.Contracts.DTOs.Expert;

namespace EduVi.Services.Expert;

public interface IExpertService
{
    // ── Expert: nộp hồ sơ ────────────────────────────────────────────────────

    /// <summary>
    /// Expert upload file chứng minh năng lực lên GCS và lưu vào DB.
    /// GCS path: expert_verifications/{expertId}/{verificationCode}{ext}
    /// </summary>
    Task<ExpertVerificationDto> UploadVerificationAsync(int expertId, UploadVerificationRequestDto request);

    /// <summary>
    /// Expert xem danh sách hồ sơ đã nộp và trạng thái của từng file.
    /// </summary>
    Task<List<ExpertVerificationDto>> GetMyVerificationsAsync(int expertId);

    /// <summary>
    /// Expert xóa hồ sơ khi bị reject và muốn nộp lại.
    /// Không cho phép xóa hồ sơ đã được approve.
    /// </summary>
    Task DeleteVerificationAsync(int expertId, string verificationCode);

    // ── Staff: kiểm duyệt hồ sơ ─────────────────────────────────────────────

    /// <summary>
    /// Staff lấy danh sách hồ sơ đang chờ duyệt.
    /// Trả về Signed URL (15 phút) để Staff xem file trực tiếp từ GCS.
    /// </summary>
    Task<List<ExpertVerificationStaffDto>> GetPendingVerificationsAsync();

    /// <summary>
    /// Staff xem chi tiết 1 hồ sơ (bao gồm Signed URL mới).
    /// </summary>
    Task<ExpertVerificationStaffDto> GetVerificationDetailAsync(string verificationCode);

    /// <summary>
    /// Staff phê duyệt hoặc từ chối hồ sơ.
    /// Khi approve tất cả: tự động set Expert.IsVerified = true.
    /// Khi reject: ghi RejectionReason, Expert.IsVerified vẫn giữ nguyên hoặc set = false.
    /// </summary>
    Task ReviewVerificationAsync(int staffId, string verificationCode, ReviewVerificationRequestDto request);
}
