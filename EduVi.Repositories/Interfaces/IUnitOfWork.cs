using EduVi.Contracts.Repositories;
using EduVi.Repositories.Interfaces;

namespace EduVi.Repositories.Interfaces;

/// <summary>
/// Unit of Work pattern - Quản lý tất cả repositories và transactions
/// Đảm bảo tất cả thao tác dùng chung 1 DbContext và có thể commit/rollback cùng nhau
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Repository xử lý Authentication
    /// </summary>
    IAuthenticationRepository AuthenticationRepository { get; }

    /// <summary>
    /// Repository xử lý Payment: Wallet, Transactions, Orders, Subscriptions
    /// </summary>
    IPaymentRepository PaymentRepository { get; }

    /// <summary>
    /// Repository xử lý Admin: User Management, Financial, Plans
    /// </summary>
    IAdminRepository AdminRepository { get; }

    /// <summary>
    /// Repository xử lý Pipeline: Products cho AI evaluation
    /// </summary>
    IPipelineRepository PipelineRepository { get; }

    /// <summary>
    /// Repository xử lý InputDocuments
    /// </summary>
    IInputDocumentRepository InputDocumentRepository { get; }

    /// <summary>
    /// Repository xử lý Curriculum: Subjects, Grades, Lessons
    /// </summary>
    ICurriculumRepository CurriculumRepository { get; }

    /// <summary>
    /// Repository xử lý Expert: upload hồ sơ, xem trạng thái, xóa hồ sơ
    /// </summary>
    IExpertRepository ExpertRepository { get; }

    /// <summary>
    /// Repository xử lý Staff: kiểm duyệt hồ sơ Expert, kiểm duyệt Materials
    /// </summary>
    IStaffRepository StaffRepository { get; }

    /// <summary>
    /// Repository xử lý Teacher: browse/mua materials, xem materials đã mua
    /// </summary>
    ITeacherRepository TeacherRepository { get; }

    /// <summary>
    /// Repository xử lý CurriculumDocuments: upload/quản lý tài liệu chương trình giáo dục
    /// </summary>
    ICurriculumDocumentRepository CurriculumDocumentRepository { get; }

    /// <summary>
    /// Repository xử lý TextbookDocuments: upload/quản lý sách giáo khoa
    /// </summary>
    ITextbookDocumentRepository TextbookDocumentRepository { get; }

    /// <summary>
    /// Repository xử lý Classrooms: quản lý lớp học và danh sách học sinh
    /// </summary>
    IClassroomRepository ClassroomRepository { get; }

    /// <summary>
    /// Repository xử lý GameTemplates
    /// </summary>
    IGameTemplateRepository GameTemplateRepository { get; }

    /// <summary>
    /// Repository xử lý WithdrawalRequests: yêu cầu rút tiền của Expert
    /// </summary>
    IWithdrawalRepository WithdrawalRepository { get; }

    // ============ Transaction Management ============

    /// <summary>
    /// Bắt đầu DB transaction. Dùng khi cần nhiều thao tác atomic.
    /// PHẢI gọi CommitTransactionAsync() hoặc RollbackTransactionAsync() sau đó.
    /// </summary>
    Task BeginTransactionAsync();

    /// <summary>
    /// Commit transaction: SaveChanges + Commit.
    /// </summary>
    Task CommitTransactionAsync();

    /// <summary>
    /// Rollback transaction khi có lỗi.
    /// </summary>
    Task RollbackTransactionAsync();

    /// <summary>
    /// Lưu thay đổi NGAY (không cần transaction wrapper).
    /// Dùng cho các thao tác đơn lẻ như tạo PENDING record.
    /// </summary>
    Task<int> SaveChangesAsync();

    // ============ Legacy (giữ lại cho code cũ) ============

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (đồng bộ)
    /// </summary>
    int SaveChangesWithTransaction();

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (bất đồng bộ)
    /// </summary>
    Task<int> SaveChangesWithTransactionAsync();
}
