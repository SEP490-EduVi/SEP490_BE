using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Contracts.Common;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class AdminRepository : IAdminRepository
{
    private readonly EduViContext _context;

    public AdminRepository(EduViContext context)
    {
        _context = context;
    }

    // ============ User Management ============

    public async Task<(List<Users> Items, int TotalCount)> GetUsersAsync(
        int? roleId, int? status, string? search,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        var query = _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .AsQueryable();

        // Bộ lọc
        if (roleId.HasValue)
            query = query.Where(u => u.RoleId == roleId.Value);

        if (status.HasValue)
            query = query.Where(u => u.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(u =>
                u.Username.ToLower().Contains(term) ||
                u.Email.ToLower().Contains(term) ||
                (u.FullName != null && u.FullName.ToLower().Contains(term)));
        }

        if (fromDate.HasValue)
            query = query.Where(u => u.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(u => u.CreatedAt <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Users?> GetUserByIdAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .Include(u => u.Wallets)
            .FirstOrDefaultAsync(u => u.UserId == userId);
    }

    public async Task<Users?> GetUserByCodeAsync(string userCode)
    {
        return await _context.Users
            .Include(u => u.Role)
            .Include(u => u.Admins)
            .Include(u => u.Experts)
            .Include(u => u.Staffs)
            .Include(u => u.Teachers)
            .Include(u => u.Wallets)
            .FirstOrDefaultAsync(u => u.UserCode == userCode);
    }

    public async Task UpdateUserAsync(Users user)
    {
        _context.Users.Update(user);
    }

    public async Task<bool> UpdateUserStatusAsync(int userId, int status)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.Status = status;
        _context.Users.Update(user);
        return true;
    }

    public async Task<int> RemoveTeacherOwnedMaterialsAsync(int teacherId)
    {
        return await _context.TeacherMaterials
            .Where(tm => tm.TeacherId == teacherId)
            .ExecuteDeleteAsync();
    }

    public async Task<int> HideApprovedMaterialsByExpertAsync(int expertId, string reason)
    {
        return await _context.Materials
            .Where(m => m.ExpertId == expertId && m.ApprovalStatus == 1)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ApprovalStatus, 3)
                .SetProperty(m => m.RejectionReason, reason));
    }

    public async Task<int> RestoreMaterialsHiddenByExpertBanAsync(int expertId, string reason)
    {
        return await _context.Materials
            .Where(m => m.ExpertId == expertId && m.ApprovalStatus == 3 && m.RejectionReason == reason)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.ApprovalStatus, 1)
                .SetProperty(m => m.RejectionReason, (string?)null));
    }

    public async Task<bool> DeleteUserAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        _context.Users.Remove(user);
        return true;
    }

    public async Task<List<Roles>> GetAllRolesAsync()
    {
        return await _context.Roles.OrderBy(r => r.RoleId).ToListAsync();
    }

    // ============ Financial ============

    public async Task<(List<Wallets> Items, int TotalCount)> GetAllWalletsAsync(int page, int pageSize)
    {
        var query = _context.Wallets
            .Include(w => w.User)
            .OrderByDescending(w => w.Balance);

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<decimal> GetTotalWalletBalanceAsync()
    {
        return await _context.Wallets.SumAsync(w => w.Balance ?? 0);
    }

    public async Task<(int Total, int Active, int Banned)> GetUserCountsAsync()
    {
        var total = await _context.Users.CountAsync();
        var active = await _context.Users.CountAsync(u => u.Status == 1 && u.IsEmailVerified == true);
        var banned = await _context.Users.CountAsync(u => u.Status == 0);
        return (total, active, banned);
    }

    public async Task<(List<WalletTransactions> Items, int TotalCount)> GetAllTransactionsAsync(
        int? userId, string? transactionType, int? status,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        var query = _context.WalletTransactions
            .Include(t => t.Wallet)
                .ThenInclude(w => w.User)
            .Include(t => t.Plan)
            .Include(t => t.Material)
            .AsQueryable();

        // Lọc theo userId thông qua Wallet
        if (userId.HasValue)
            query = query.Where(t => t.Wallet != null && t.Wallet.UserId == userId.Value);

        if (!string.IsNullOrWhiteSpace(transactionType))
            query = query.Where(t => t.TransactionType == transactionType);

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        if (fromDate.HasValue)
            query = query.Where(t => t.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(t => t.CreatedAt <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(decimal TotalAmount, int Count)> GetTopUpStatsAsync()
    {
        var completed = _context.WalletTransactions
            .Where(t => t.TransactionType == WalletTransactionTypeConstants.TopUp && t.Status == 1);

        var count = await completed.CountAsync();
        var total = count > 0 ? await completed.SumAsync(t => t.Amount ?? 0) : 0;

        return (total, count);
    }

    public async Task<(decimal TotalAmount, int Count)> GetSubscriptionStatsAsync()
    {
        var completed = _context.WalletTransactions
            .Where(t => t.TransactionType == WalletTransactionTypeConstants.BuySubscription && t.Status == 1);

        var count = await completed.CountAsync();
        // BuySubscription Amount là số âm → lấy Math.Abs
        var total = count > 0 ? Math.Abs(await completed.SumAsync(t => t.Amount ?? 0)) : 0;

        return (total, count);
    }

    public async Task<(List<MaterialSalesAnalyticsRow> Items, int TotalCount)> GetMaterialSalesAnalyticsAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode,
        int page,
        int pageSize)
    {
        var filteredTransactions = BuildMaterialSalesTransactionQuery(fromDate, toDate, subjectCode, gradeCode, expertCode, materialCode);

        var groupedQuery = filteredTransactions
            .GroupBy(transaction => new
            {
                MaterialCode = transaction.Material != null ? transaction.Material.MaterialCode : string.Empty,
                Title = transaction.Material != null ? transaction.Material.Title : string.Empty,
                SubjectCode = transaction.Material != null && transaction.Material.Subject != null ? transaction.Material.Subject.SubjectCode : null,
                GradeCode = transaction.Material != null && transaction.Material.Grade != null ? transaction.Material.Grade.GradeCode : null,
                ExpertCode = transaction.Material != null && transaction.Material.Expert != null ? transaction.Material.Expert.ExpertCode : null,
                ExpertName = transaction.Material != null && transaction.Material.Expert != null && transaction.Material.Expert.Expert != null
                    ? transaction.Material.Expert.Expert.FullName
                    : null
            })
            .Select(group => new MaterialSalesAnalyticsRow
            {
                MaterialCode = group.Key.MaterialCode,
                Title = group.Key.Title,
                SubjectCode = group.Key.SubjectCode,
                GradeCode = group.Key.GradeCode,
                ExpertCode = group.Key.ExpertCode,
                ExpertName = group.Key.ExpertName,
                SoldCount = group.Count(),
                UniqueBuyerCount = group.Select(transaction => transaction.Wallet != null ? transaction.Wallet.UserId : null)
                    .Distinct()
                    .Count(userId => userId != null),
                GrossRevenue = group.Sum(transaction => Math.Abs(transaction.Amount ?? 0)),
                LastPurchasedDate = group.Max(transaction => transaction.CreatedAt)
            });

        var totalCount = await groupedQuery.CountAsync();

        var items = await groupedQuery
            .OrderByDescending(item => item.GrossRevenue)
            .ThenByDescending(item => item.SoldCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<ExpertSalesAnalyticsRow> Items, int TotalCount)> GetExpertSalesAnalyticsAsync(
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode,
        int page,
        int pageSize)
    {
        var filteredTransactions = BuildMaterialSalesTransactionQuery(fromDate, toDate, subjectCode, gradeCode, expertCode, materialCode)
            .Where(transaction => transaction.Material != null && transaction.Material.Expert != null);

        var groupedQuery = filteredTransactions
            .GroupBy(transaction => new
            {
                ExpertCode = transaction.Material != null && transaction.Material.Expert != null ? transaction.Material.Expert.ExpertCode : string.Empty,
                ExpertName = transaction.Material != null && transaction.Material.Expert != null && transaction.Material.Expert.Expert != null
                    ? transaction.Material.Expert.Expert.FullName
                    : string.Empty
            })
            .Select(group => new ExpertSalesAnalyticsRow
            {
                ExpertCode = group.Key.ExpertCode,
                ExpertName = group.Key.ExpertName,
                SoldMaterialCount = group.Select(transaction => transaction.MaterialId).Distinct().Count(materialId => materialId != null),
                SoldCount = group.Count(),
                UniqueBuyerCount = group.Select(transaction => transaction.Wallet != null ? transaction.Wallet.UserId : null)
                    .Distinct()
                    .Count(userId => userId != null),
                GrossRevenue = group.Sum(transaction => Math.Abs(transaction.Amount ?? 0)),
                LastPurchasedDate = group.Max(transaction => transaction.CreatedAt)
            });

        var totalCount = await groupedQuery.CountAsync();

        var items = await groupedQuery
            .OrderByDescending(item => item.GrossRevenue)
            .ThenByDescending(item => item.SoldCount)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<RevenueForecastAnalyticsRow> GetRevenueForecastAnalyticsAsync(
        DateTime currentFromDate,
        DateTime currentToDate,
        DateTime previousFromDate,
        DateTime previousToDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode)
    {
        var currentQuery = BuildMaterialSalesTransactionQuery(currentFromDate, currentToDate, subjectCode, gradeCode, expertCode, materialCode);
        var previousQuery = BuildMaterialSalesTransactionQuery(previousFromDate, previousToDate, subjectCode, gradeCode, expertCode, materialCode);

        var currentRevenue = await currentQuery.SumAsync(transaction => Math.Abs(transaction.Amount ?? 0));
        var previousRevenue = await previousQuery.SumAsync(transaction => Math.Abs(transaction.Amount ?? 0));

        var currentSoldCount = await currentQuery.CountAsync();
        var previousSoldCount = await previousQuery.CountAsync();

        var currentUniqueBuyerCount = await currentQuery
            .Select(transaction => transaction.Wallet != null ? transaction.Wallet.UserId : null)
            .Distinct()
            .CountAsync(userId => userId != null);

        var previousUniqueBuyerCount = await previousQuery
            .Select(transaction => transaction.Wallet != null ? transaction.Wallet.UserId : null)
            .Distinct()
            .CountAsync(userId => userId != null);

        return new RevenueForecastAnalyticsRow
        {
            CurrentRevenue = currentRevenue,
            PreviousRevenue = previousRevenue,
            CurrentSoldCount = currentSoldCount,
            PreviousSoldCount = previousSoldCount,
            CurrentUniqueBuyerCount = currentUniqueBuyerCount,
            PreviousUniqueBuyerCount = previousUniqueBuyerCount
        };
    }

    private IQueryable<WalletTransactions> BuildMaterialSalesTransactionQuery(
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? materialCode)
    {
        var adminMaterialIncomeTransactionTypes = WalletTransactionTypeConstants.AdminMaterialIncomeTransactionTypes;

        var query = _context.WalletTransactions
            .Include(transaction => transaction.Wallet)
            .Include(transaction => transaction.Material)
                .ThenInclude(material => material.Subject)
            .Include(transaction => transaction.Material)
                .ThenInclude(material => material.Grade)
            .Include(transaction => transaction.Material)
                .ThenInclude(material => material.Expert)
                    .ThenInclude(expert => expert.Expert)
            .Where(transaction => adminMaterialIncomeTransactionTypes.Contains(transaction.TransactionType)
                && transaction.Status == 1)
            .AsQueryable();

        if (fromDate.HasValue)
            query = query.Where(transaction => transaction.CreatedAt >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(transaction => transaction.CreatedAt <= toDate.Value);

        if (!string.IsNullOrWhiteSpace(subjectCode))
            query = query.Where(transaction => transaction.Material != null
                && transaction.Material.Subject != null
                && transaction.Material.Subject.SubjectCode == subjectCode);

        if (!string.IsNullOrWhiteSpace(gradeCode))
            query = query.Where(transaction => transaction.Material != null
                && transaction.Material.Grade != null
                && transaction.Material.Grade.GradeCode == gradeCode);

        if (!string.IsNullOrWhiteSpace(expertCode))
            query = query.Where(transaction => transaction.Material != null
                && transaction.Material.Expert != null
                && transaction.Material.Expert.ExpertCode == expertCode);

        if (!string.IsNullOrWhiteSpace(materialCode))
            query = query.Where(transaction => transaction.Material != null
                && transaction.Material.MaterialCode == materialCode);

        return query;
    }

    // ============ Orders ============

    public async Task<(List<Orders> Items, int TotalCount)> GetAllOrdersAsync(
        int? teacherId, string? orderType, int? status, string? paymentMethod,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        var query = _context.Orders
            .Include(o => o.Teacher)
                .ThenInclude(t => t.Teacher) // Teacher → Users
            .AsQueryable();

        if (teacherId.HasValue)
            query = query.Where(o => o.TeacherId == teacherId.Value);

        if (!string.IsNullOrWhiteSpace(orderType))
            query = query.Where(o => o.OrderType == orderType);

        if (status.HasValue)
            query = query.Where(o => o.Status == status.Value);

        if (!string.IsNullOrWhiteSpace(paymentMethod))
            query = query.Where(o => o.PaymentMethod == paymentMethod);

        if (fromDate.HasValue)
            query = query.Where(o => o.OrderDate >= fromDate.Value);

        if (toDate.HasValue)
            query = query.Where(o => o.OrderDate <= toDate.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(int Total, int Completed)> GetOrderCountsAsync()
    {
        var total = await _context.Orders.CountAsync();
        var completed = await _context.Orders.CountAsync(o => o.Status == 1);
        return (total, completed);
    }

    // ============ Quota Plans ============

    public async Task<List<QuotaPlans>> GetAllPlansAsync()
    {
        return await _context.QuotaPlans
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<QuotaPlans?> GetPlanByIdAsync(int planId)
    {
        return await _context.QuotaPlans
            .FirstOrDefaultAsync(p => p.PlanId == planId);
    }

    public async Task<QuotaPlans> CreatePlanAsync(QuotaPlans plan)
    {
        await _context.QuotaPlans.AddAsync(plan);
        return plan;
    }

    public async Task UpdatePlanAsync(QuotaPlans plan)
    {
        _context.QuotaPlans.Update(plan);
    }

    public async Task<bool> DeletePlanAsync(int planId)
    {
        var plan = await _context.QuotaPlans.FindAsync(planId);
        if (plan == null) return false;

        // Soft delete: chỉ ẩn, không xóa
        plan.IsActive = false;
        _context.QuotaPlans.Update(plan);
        return true;
    }

    // ============ Materials (Admin CRUD) ============

    public async Task<(List<Materials> Items, int TotalCount)> GetMaterialsForAdminAsync(
        int? approvalStatus,
        string? type,
        string? subjectCode,
        string? gradeCode,
        string? expertCode,
        string? search,
        int page,
        int pageSize)
    {
        var query = _context.Materials
            .Include(material => material.Subject)
            .Include(material => material.Grade)
            .Include(material => material.Expert)
                .ThenInclude(expert => expert.Expert)
            .AsQueryable();

        if (approvalStatus.HasValue)
            query = query.Where(material => material.ApprovalStatus == approvalStatus.Value);

        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            query = query.Where(material => material.Type == normalizedType);
        }

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            var normalizedSubjectCode = subjectCode.Trim();
            query = query.Where(material => material.Subject != null && material.Subject.SubjectCode == normalizedSubjectCode);
        }

        if (!string.IsNullOrWhiteSpace(gradeCode))
        {
            var normalizedGradeCode = gradeCode.Trim();
            query = query.Where(material => material.Grade != null && material.Grade.GradeCode == normalizedGradeCode);
        }

        if (!string.IsNullOrWhiteSpace(expertCode))
        {
            var normalizedExpertCode = expertCode.Trim();
            query = query.Where(material => material.Expert != null && material.Expert.ExpertCode == normalizedExpertCode);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearchTerm = search.Trim().ToLower();
            query = query.Where(material =>
                material.MaterialCode.ToLower().Contains(normalizedSearchTerm)
                || material.Title.ToLower().Contains(normalizedSearchTerm));
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(material => material.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<Materials?> GetMaterialByCodeWithDetailsAsync(string materialCode, bool asNoTracking = false)
    {
        var query = _context.Materials
            .Include(material => material.Subject)
            .Include(material => material.Grade)
            .Include(material => material.Expert)
                .ThenInclude(expert => expert.Expert)
            .AsQueryable();

        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        else
        {
            query = query.AsTracking();
        }

        return await query.FirstOrDefaultAsync(material => material.MaterialCode == materialCode);
    }

    public async Task<bool> MaterialCodeExistsAsync(string materialCode)
    {
        return await _context.Materials.AnyAsync(material => material.MaterialCode == materialCode);
    }

    public async Task<Experts?> GetExpertByCodeAsync(string expertCode)
    {
        return await _context.Experts
            .Include(expert => expert.Expert)
            .FirstOrDefaultAsync(expert => expert.ExpertCode == expertCode);
    }

    public async Task<Subjects?> GetSubjectByCodeAsync(string subjectCode)
    {
        return await _context.Subjects.FirstOrDefaultAsync(subject => subject.SubjectCode == subjectCode);
    }

    public async Task<Grades?> GetGradeByCodeAsync(string gradeCode)
    {
        return await _context.Grades.FirstOrDefaultAsync(grade => grade.GradeCode == gradeCode);
    }

    public async Task<Materials> CreateMaterialAsync(Materials material)
    {
        var entry = await _context.Materials.AddAsync(material);
        return entry.Entity;
    }

    public void UpdateMaterial(Materials material)
    {
        _context.Materials.Attach(material);
        _context.Entry(material).State = EntityState.Modified;
    }

    public void DeleteMaterial(Materials material)
    {
        _context.Materials.Remove(material);
    }

    public async Task<bool> HasMaterialDependenciesAsync(int materialId)
    {
        var hasTeacherMaterialDependency = await _context.TeacherMaterials.AnyAsync(teacherMaterial => teacherMaterial.MaterialId == materialId);
        if (hasTeacherMaterialDependency)
            return true;

        var hasWalletTransactionDependency = await _context.WalletTransactions.AnyAsync(walletTransaction => walletTransaction.MaterialId == materialId);
        if (hasWalletTransactionDependency)
            return true;

        var hasProductMaterialDependency = await _context.ProductMaterials.AnyAsync(productMaterial => productMaterial.MaterialId == materialId);
        return hasProductMaterialDependency;
    }

    // ── Platform Wallet ─────────────────────────────────────────────────────────

    public async Task<Wallets?> GetAdminWalletAsync()
    {
        return await _context.Wallets
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.User != null && w.User.RoleId == 1);
    }

    public void UpdateWallet(Wallets wallet)
    {
        _context.Wallets.Update(wallet);
    }

    public async Task CreateWalletTransactionAsync(WalletTransactions transaction)
    {
        await _context.WalletTransactions.AddAsync(transaction);
    }
}
