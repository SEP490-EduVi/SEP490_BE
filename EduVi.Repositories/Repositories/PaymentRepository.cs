using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class PaymentRepository : IPaymentRepository
{
    private readonly EduViContext _context;

    public PaymentRepository(EduViContext context)
    {
        _context = context;
    }

    // ============ Wallet ============

    public async Task<Wallets?> GetWalletByUserIdAsync(int userId)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId);
    }

    public async Task<Wallets?> GetWalletByIdAsync(int walletId)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.WalletId == walletId);
    }

    public async Task<Wallets> CreateWalletAsync(Wallets wallet)
    {
        await _context.Wallets.AddAsync(wallet);
        // Không gọi SaveChanges — để UnitOfWork quản lý
        return wallet;
    }

    public async Task UpdateWalletBalanceAsync(int walletId, decimal newBalance)
    {
        var wallet = await _context.Wallets.FindAsync(walletId);
        if (wallet != null)
        {
            wallet.Balance = newBalance;
            wallet.LastUpdated = DateTime.UtcNow;

            // Context dùng NoTracking mặc định → FindAsync cũng không track entity.
            // Cần gọi Update() để attach + đánh dấu Modified, SaveChanges mới ghi DB.
            _context.Wallets.Update(wallet);
        }
    }

    // ============ WalletTransactions ============

    public async Task<bool> IsOrderCodeCompletedAsync(long orderCode)
    {
        return await _context.WalletTransactions
            .AnyAsync(t => t.OrderCode == orderCode && t.Status == 1); // 1 = COMPLETED
    }

    public async Task<WalletTransactions?> GetTransactionByOrderCodeAsync(long orderCode)
    {
        return await _context.WalletTransactions
            .FirstOrDefaultAsync(t => t.OrderCode == orderCode);
    }

    public async Task<WalletTransactions> CreateTransactionAsync(WalletTransactions transaction)
    {
        await _context.WalletTransactions.AddAsync(transaction);
        // Không gọi SaveChanges — để UnitOfWork quản lý
        return transaction;
    }

    public async Task UpdateTransactionAsync(WalletTransactions transaction)
    {
        _context.WalletTransactions.Update(transaction);
        // Không gọi SaveChanges — để UnitOfWork quản lý
    }

    public async Task<(List<WalletTransactions> Items, int TotalCount)> GetTransactionsByWalletIdAsync(
        int walletId, int page, int pageSize)
    {
        var query = _context.WalletTransactions
            .Where(t => t.WalletId == walletId)
            .OrderByDescending(t => t.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    // ============ SubscriptionPlans ============

    public async Task<List<SubscriptionPlans>> GetAllActivePlansAsync()
    {
        return await _context.SubscriptionPlans
            .Where(p => p.IsActive == true)
            .OrderBy(p => p.Price)
            .ToListAsync();
    }

    public async Task<SubscriptionPlans?> GetPlanByIdAsync(int planId)
    {
        return await _context.SubscriptionPlans
            .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive == true);
    }

    // ============ Orders ============

    public async Task<Orders> CreateOrderAsync(Orders order)
    {
        await _context.Orders.AddAsync(order);
        // Không gọi SaveChanges — để UnitOfWork quản lý
        return order;
    }

    // ============ UserQuotas ============

    public async Task<UserQuotas?> GetQuotaByTeacherIdAsync(int teacherId)
    {
        return await _context.UserQuotas
            .FirstOrDefaultAsync(q => q.TeacherId == teacherId);
    }

    public async Task<UserQuotas> CreateOrUpdateQuotaAsync(int teacherId, int quotaToAdd)
    {
        var existing = await _context.UserQuotas
            .FirstOrDefaultAsync(q => q.TeacherId == teacherId);

        if (existing == null)
        {
            var quota = new UserQuotas
            {
                TeacherId = teacherId,
                TotalQuota = quotaToAdd,
                AvailableQuota = quotaToAdd,
                UsedQuota = 0,
                UpdatedAt = DateTime.UtcNow
            };
            await _context.UserQuotas.AddAsync(quota);
            return quota;
        }
        else
        {
            existing.TotalQuota = (existing.TotalQuota ?? 0) + quotaToAdd;
            existing.AvailableQuota = (existing.AvailableQuota ?? 0) + quotaToAdd;
            existing.UpdatedAt = DateTime.UtcNow;
            // Context dùng NoTracking mặc định → cần gọi Update() để attach + mark modified
            _context.UserQuotas.Update(existing);
            return existing;
        }
    }
}
