using EduVi.Contracts.Repositories;

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

    // TODO: Thêm các repositories khác ở đây khi cần
    // IUserRepository UserRepository { get; }
    // IMaterialRepository MaterialRepository { get; }
    // IOrderRepository OrderRepository { get; }

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (đồng bộ)
    /// </summary>
    /// <returns>Số bản ghi bị ảnh hưởng, -1 nếu có lỗi</returns>
    int SaveChangesWithTransaction();

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (bất đồng bộ)
    /// </summary>
    /// <returns>Số bản ghi bị ảnh hưởng, -1 nếu có lỗi</returns>
    Task<int> SaveChangesWithTransactionAsync();
}
