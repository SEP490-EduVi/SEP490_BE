using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class WithdrawalRepository : IWithdrawalRepository
{
    private readonly EduViContext _context;

    public WithdrawalRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<WithdrawalRequests> CreateAsync(WithdrawalRequests request)
    {
        await _context.WithdrawalRequests.AddAsync(request);
        return request;
    }

    public async Task<WithdrawalRequests?> GetByIdAsync(int withdrawalId)
    {
        return await _context.WithdrawalRequests
            .Include(w => w.User)
            .FirstOrDefaultAsync(w => w.WithdrawalId == withdrawalId);
    }

    public async Task<(List<WithdrawalRequests> Items, int TotalCount)> GetByUserIdAsync(
        int userId, int page, int pageSize)
    {
        var query = _context.WithdrawalRequests
            .Where(w => w.UserId == userId)
            .OrderByDescending(w => w.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<(List<WithdrawalRequests> Items, int TotalCount)> GetAllAsync(
        string? status, int page, int pageSize)
    {
        var query = _context.WithdrawalRequests
            .Include(w => w.User)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(w => w.Status == status);

        query = query.OrderByDescending(w => w.CreatedAt);

        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public Task UpdateAsync(WithdrawalRequests request)
    {
        _context.WithdrawalRequests.Update(request);
        return Task.CompletedTask;
    }
}
