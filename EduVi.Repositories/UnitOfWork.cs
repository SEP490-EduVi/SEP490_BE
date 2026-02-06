using EduVi.Contracts.Repositories;
using EduVi.Repositories.DBContext;
using EduVi.Repositories.Repositories;
using EduVi.Repositories.Interfaces;

namespace EduVi.Repositories;

/// <summary>
/// Unit of Work - Quản lý tất cả repositories và database transactions
/// 
/// LỢI ÍCH:
/// 1. QUẢN LÝ TRANSACTION TẬP TRUNG: Tất cả thay đổi từ nhiều repositories có thể commit/rollback cùng nhau
/// 2. ĐẢM BẢO DATA CONSISTENCY: Nếu 1 thao tác fail, tất cả đều rollback
/// 3. SINGLE DbContext INSTANCE: Tất cả repositories dùng chung 1 context, tránh conflict
/// 4. GIẢM COUPLING: Service chỉ cần inject UnitOfWork thay vì inject từng repository
/// 5. DỄ TEST: Mock UnitOfWork dễ hơn mock nhiều repositories riêng lẻ
/// 
/// VÍ DỤ SỬ DỤNG:
/// - Tạo user mới và ghi log cùng lúc
/// - Tạo order và cập nhật wallet balance
/// - Nếu 1 trong 2 fail thì cả 2 đều rollback
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly EduViContext _context;
    private AuthenticationRepository? _authenticationRepository;

    // TODO: Thêm các repositories khác khi cần
    // private UserRepository? _userRepository;
    // private MaterialRepository? _materialRepository;
    // private OrderRepository? _orderRepository;

    public UnitOfWork(EduViContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Lazy loading pattern - Repository chỉ được khởi tạo khi cần
    /// Dùng toán tử ??= để tạo instance lần đầu, các lần sau dùng lại instance cũ
    /// </summary>
    public IAuthenticationRepository AuthenticationRepository 
    { 
        get => _authenticationRepository ??= new AuthenticationRepository(_context); 
    }

    // TODO: Thêm properties cho repositories khác
    // public IUserRepository UserRepository 
    // { 
    //     get => _userRepository ??= new UserRepository(_context); 
    // }

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (đồng bộ)
    /// Nếu có lỗi sẽ rollback và trả về -1
    /// </summary>
    public int SaveChangesWithTransaction()
    {
        int result = -1;

        using (var dbContextTransaction = _context.Database.BeginTransaction())
        {
            try
            {
                result = _context.SaveChanges();
                dbContextTransaction.Commit();
            }
            catch (Exception)
            {
                result = -1;
                dbContextTransaction.Rollback();
            }
        }

        return result;
    }

    /// <summary>
    /// Lưu tất cả thay đổi với transaction (bất đồng bộ)
    /// Nếu có lỗi sẽ rollback và trả về -1
    /// </summary>
    public async Task<int> SaveChangesWithTransactionAsync()
    {
        int result = -1;

        using (var dbContextTransaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                result = await _context.SaveChangesAsync();
                await dbContextTransaction.CommitAsync();
            }
            catch (Exception)
            {
                result = -1;
                await dbContextTransaction.RollbackAsync();
            }
        }

        return result;
    }
}
