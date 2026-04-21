using EduVi.Contracts.DTOs.Material;

namespace EduVi.Services.Material;

/// <summary>
/// Service xử lý Material — bao gồm cả 3 role: Expert upload, Staff duyệt, Teacher browse/mua.
/// </summary>
public interface IMaterialService
{
    // ── Expert: quản lý materials ──────────────────────────────────────────────

    /// <summary>
    /// Expert upload học liệu dạng FILE (image | video) lên GCS.
    /// </summary>
    Task<MaterialResponseDto> UploadFileMaterialAsync(int expertId, UploadFileMaterialRequestDto request);

    /// <summary>
    /// Expert xem danh sách materials đã upload.
    /// </summary>
    Task<List<MaterialResponseDto>> GetMyMaterialsAsync(int expertId);

    /// <summary>
    /// Expert cập nhật material (chỉ được khi chưa approve).
    /// </summary>
    Task<MaterialResponseDto> UpdateMaterialAsync(int expertId, string materialCode, UpdateMaterialRequestDto request);

    /// <summary>
    /// Expert xóa material (chỉ được khi chưa approve).
    /// </summary>
    Task DeleteMaterialAsync(int expertId, string materialCode);

    // ── Staff: kiểm duyệt materials ──────────────────────────────────────────

    /// <summary>
    /// Staff lấy danh sách materials đang chờ duyệt.
    /// </summary>
    Task<List<MaterialResponseDto>> GetPendingMaterialsAsync();

    /// <summary>
    /// Staff xem chi tiết 1 material (kèm Resource URL để review nội dung).
    /// </summary>
    Task<MaterialResponseDto> GetMaterialDetailForStaffAsync(string materialCode);

    /// <summary>
    /// Staff phê duyệt hoặc từ chối material.
    /// </summary>
    Task ReviewMaterialAsync(int staffId, string materialCode, ReviewMaterialRequestDto request);

    // ── Teacher: browse và mua materials ──────────────────────────────────────

    /// <summary>
    /// Teacher browse danh sách materials đã duyệt (có lọc).
    /// </summary>
    Task<List<MaterialResponseDto>> BrowseMaterialsAsync(string? subjectCode, string? gradeCode, string? type, string? keyword);

    /// <summary>
    /// Teacher xem chi tiết 1 material (chưa mua → không có ResourceUrl).
    /// </summary>
    Task<MaterialResponseDto> GetMaterialDetailForTeacherAsync(int teacherId, string materialCode);

    /// <summary>
    /// Teacher mua material — trừ tiền ví, tạo TeacherMaterials record.
    /// </summary>
    Task<PurchasedMaterialResponseDto> PurchaseMaterialAsync(int teacherId, string materialCode);

    /// <summary>
    /// Teacher xem danh sách materials đã mua.
    /// </summary>
    Task<List<PurchasedMaterialResponseDto>> GetPurchasedMaterialsAsync(int teacherId);

    /// <summary>
    /// Teacher xem chi tiết 1 material đã mua, kể cả khi material không còn hiển thị ở marketplace.
    /// </summary>
    Task<PurchasedMaterialResponseDto> GetPurchasedMaterialDetailAsync(int teacherId, string materialCode);
}
