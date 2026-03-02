using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
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

    public async Task<bool> ChangeUserRoleAsync(int userId, int newRoleId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user == null) return false;

        user.RoleId = newRoleId;
        _context.Users.Update(user);
        return true;
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

    public async Task<bool> RoleExistsAsync(int roleId)
    {
        return await _context.Roles.AnyAsync(r => r.RoleId == roleId);
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
        var active = await _context.Users.CountAsync(u => u.Status == 1);
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
            .Where(t => t.TransactionType == "TOP_UP" && t.Status == 1);

        var count = await completed.CountAsync();
        var total = count > 0 ? await completed.SumAsync(t => t.Amount ?? 0) : 0;

        return (total, count);
    }

    public async Task<(decimal TotalAmount, int Count)> GetSubscriptionStatsAsync()
    {
        var completed = _context.WalletTransactions
            .Where(t => t.TransactionType == "BUY_SUBSCRIPTION" && t.Status == 1);

        var count = await completed.CountAsync();
        // BuySubscription Amount là số âm → lấy Math.Abs
        var total = count > 0 ? Math.Abs(await completed.SumAsync(t => t.Amount ?? 0)) : 0;

        return (total, count);
    }

    // ============ Orders ============

    public async Task<(List<Orders> Items, int TotalCount)> GetAllOrdersAsync(
        int? teacherId, int? status, string? paymentMethod,
        DateTime? fromDate, DateTime? toDate,
        int page, int pageSize)
    {
        var query = _context.Orders
            .Include(o => o.Teacher)
                .ThenInclude(t => t.Teacher) // Teacher → Users
            .AsQueryable();

        if (teacherId.HasValue)
            query = query.Where(o => o.TeacherId == teacherId.Value);

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

    // ============ Subscription Plans ============

    public async Task<List<SubscriptionPlans>> GetAllPlansAsync()
    {
        return await _context.SubscriptionPlans
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<SubscriptionPlans?> GetPlanByIdAsync(int planId)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanId == planId);
    }

    public async Task<SubscriptionPlans> CreatePlanAsync(SubscriptionPlans plan)
    {
        await _context.SubscriptionPlans.AddAsync(plan);
        return plan;
    }

    public async Task UpdatePlanAsync(SubscriptionPlans plan)
    {
        _context.SubscriptionPlans.Update(plan);
    }

    public async Task<bool> DeletePlanAsync(int planId)
    {
        var plan = await _context.SubscriptionPlans.FindAsync(planId);
        if (plan == null) return false;

        // Soft delete: chỉ ẩn, không xóa
        plan.IsActive = false;
        _context.SubscriptionPlans.Update(plan);
        return true;
    }
}
