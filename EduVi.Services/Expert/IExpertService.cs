using EduVi.Contracts.DTOs.Expert;
using EduVi.Contracts.DTOs.Profile;

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

    /// <summary>
    /// Expert stream file đã nộp về client để xem lại. Kiểm tra ownership trước khi trả file.
    /// </summary>
    Task<ExpertVerificationFileDto> GetMyVerificationFileAsync(int expertId, string verificationCode);

    // ── Staff: kiểm duyệt hồ sơ ─────────────────────────────────────────────

    /// <summary>
    /// Staff lấy danh sách hồ sơ đang chờ duyệt.
    /// Trả về URL proxy nội bộ để Staff xem file qua backend.
    /// </summary>
    Task<List<ExpertVerificationStaffDto>> GetPendingVerificationsAsync();

    /// <summary>
    /// Lấy nội dung file verification để controller stream cho Staff.
    /// </summary>
    Task<ExpertVerificationFileDto> GetVerificationFileAsync(string verificationCode);

    /// <summary>
    /// Staff phê duyệt hoặc từ chối hồ sơ.
    /// Khi approve tất cả: tự động set Expert.IsVerified = true.
    /// Khi reject: ghi RejectionReason, Expert.IsVerified vẫn giữ nguyên hoặc set = false.
    /// </summary>
    Task ReviewVerificationAsync(int staffId, string verificationCode, ReviewVerificationRequestDto request);

    // ── Expert: profile ───────────────────────────────────────────────────────

    /// <summary>
    /// Lấy thông tin profile của Expert đang đăng nhập.
    /// </summary>
    Task<ExpertProfileResponse> GetProfileAsync(int userId);

    /// <summary>
    /// Cập nhật thông tin profile (FullName, PhoneNumber, Bio).
    /// </summary>
    Task UpdateProfileAsync(int userId, UpdateExpertProfileRequest request);

    /// <summary>
    /// Expert xem doanh số theo từng material của chính mình.
    /// </summary>
    Task<List<ExpertMaterialSalesResponse>> GetMaterialSalesAsync(int expertId, ExpertSalesFilterRequest filter);

    /// <summary>
    /// Expert xem tổng quan doanh số và dự báo doanh thu.
    /// </summary>
    Task<ExpertSalesOverviewResponse> GetSalesOverviewAsync(int expertId, ExpertSalesFilterRequest filter);
}
