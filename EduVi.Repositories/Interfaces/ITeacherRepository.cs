using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

/// <summary>
/// Repository xử lý Teacher: browse/mua materials, xem materials đã mua
/// </summary>
public interface ITeacherRepository
{
    // ── Browse Materials ───────────────────────────────────────────────────────

    /// <summary>
    /// Lấy danh sách materials đã duyệt (ApprovalStatus = 1) cho Teacher browse/mua.
    /// Có thể lọc theo SubjectCode, GradeCode, Type, keyword.
    /// </summary>
    Task<List<Materials>> GetApprovedMaterialsAsync(string? subjectCode, string? gradeCode, string? type, string? keyword);

    /// <summary>
    /// Lấy chi tiết 1 material đã duyệt theo code.
    /// </summary>
    Task<Materials?> GetApprovedMaterialByCodeAsync(string materialCode);

    // ── Purchase ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra Teacher đã mua material này chưa.
    /// </summary>
    Task<bool> HasTeacherPurchasedAsync(int teacherId, int materialId);

    /// <summary>
    /// Tạo bản ghi TeacherMaterials khi Teacher mua thành công.
    /// </summary>
    Task CreateTeacherMaterialAsync(TeacherMaterials teacherMaterial);

    /// <summary>
    /// Lấy danh sách materials đã mua của Teacher, kèm Material → Expert, Subject, Grade navigation.
    /// </summary>
    Task<List<TeacherMaterials>> GetPurchasedMaterialsAsync(int teacherId);

    /// <summary>
    /// Lấy chi tiết 1 material đã mua theo MaterialCode của Teacher.
    /// Không phụ thuộc ApprovalStatus marketplace.
    /// </summary>
    Task<TeacherMaterials?> GetPurchasedMaterialByCodeAsync(int teacherId, string materialCode);

    // ── Wallet ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy Wallet của Teacher để trừ tiền khi mua.
    /// </summary>
    Task<Wallets?> GetWalletByUserIdAsync(int userId);

    /// <summary>
    /// Cập nhật số dư ví sau khi mua.
    /// </summary>
    void UpdateWallet(Wallets wallet);

    /// <summary>
    /// Tạo giao dịch ví (BUY_MATERIAL).
    /// </summary>
    Task CreateWalletTransactionAsync(WalletTransactions transaction);

    // ── Profile ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Lấy thông tin Teacher kèm Users navigation để hiển thị hoặc cập nhật profile.
    /// TeacherId = UserId (FK).
    /// </summary>
    Task<Teachers?> GetProfileByUserIdAsync(int userId);
}
