using EduVi.Contracts.Repositories;
using EduVi.Repositories.DBContext;
using EduVi.Repositories.Repositories;
using EduVi.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduVi.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly EduViContext _context;
    private AuthenticationRepository? _authenticationRepository;
    private PaymentRepository? _paymentRepository;
    private AdminRepository? _adminRepository;
    private PipelineRepository? _pipelineRepository;
    private InputDocumentRepository? _inputDocumentRepository;
    private CurriculumRepository? _curriculumRepository;
    private ExpertRepository? _expertRepository;
    private StaffRepository? _staffRepository;
    private TeacherRepository? _teacherRepository;
    private IDbContextTransaction? _currentTransaction;

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

    public IPaymentRepository PaymentRepository
    {
        get => _paymentRepository ??= new PaymentRepository(_context);
    }

    public IAdminRepository AdminRepository
    {
        get => _adminRepository ??= new AdminRepository(_context);
    }

    public IPipelineRepository PipelineRepository
    {
        get => _pipelineRepository ??= new PipelineRepository(_context);
    }

    public IInputDocumentRepository InputDocumentRepository
    {
        get => _inputDocumentRepository ??= new InputDocumentRepository(_context);
    }

    public ICurriculumRepository CurriculumRepository
    {
        get => _curriculumRepository ??= new CurriculumRepository(_context);
    }

    public IExpertRepository ExpertRepository
    {
        get => _expertRepository ??= new ExpertRepository(_context);
    }

    public IStaffRepository StaffRepository
    {
        get => _staffRepository ??= new StaffRepository(_context);
    }

    public ITeacherRepository TeacherRepository
    {
        get => _teacherRepository ??= new TeacherRepository(_context);
    }

    // ============ Transaction Management ============

    public async Task BeginTransactionAsync()
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("A transaction is already in progress.");

        _currentTransaction = await _context.Database.BeginTransactionAsync();
    }

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("No transaction in progress.");

        try
        {
            await _context.SaveChangesAsync();
            await _currentTransaction.CommitAsync();
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task RollbackTransactionAsync()
    {
        if (_currentTransaction == null) return;

        try
        {
            await _currentTransaction.RollbackAsync();
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    // ============ Legacy ============

    public int SaveChangesWithTransaction()
    {
        int result = -1;

        using (var databaseContextTransaction = _context.Database.BeginTransaction())
        {
            try
            {
                result = _context.SaveChanges();
                databaseContextTransaction.Commit();
            }
            catch (Exception)
            {
                result = -1;
                databaseContextTransaction.Rollback();
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

        using (var databaseContextTransaction = await _context.Database.BeginTransactionAsync())
        {
            try
            {
                result = await _context.SaveChangesAsync();
                await databaseContextTransaction.CommitAsync();
            }
            catch (Exception)
            {
                result = -1;
                await databaseContextTransaction.RollbackAsync();
            }
        }

        return result;
    }
}
