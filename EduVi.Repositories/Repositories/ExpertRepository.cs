using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class ExpertRepository : IExpertRepository
{
    private readonly EduViContext _context;

    public ExpertRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task CreateVerificationAsync(ExpertVerifications verification)
    {
        await _context.ExpertVerifications.AddAsync(verification);
    }

    public async Task<List<ExpertVerifications>> GetVerificationsByExpertAsync(int expertId)
    {
        return await _context.ExpertVerifications
            .Where(v => v.ExpertId == expertId)
            .OrderByDescending(v => v.UploadedAt)
            .ToListAsync();
    }

    /// <summary>
    /// Lấy hồ sơ theo code — Expert dùng khi xóa (chỉ cần ExpertId để kiểm tra quyền sở hữu, không cần navigation).
    /// </summary>
    public async Task<ExpertVerifications?> GetVerificationByCodeAsync(string verificationCode)
    {
        return await _context.ExpertVerifications
            .FirstOrDefaultAsync(v => v.VerificationCode == verificationCode);
    }

    public void DeleteVerification(ExpertVerifications verification)
    {
        _context.ExpertVerifications.Remove(verification);
    }

    public async Task<Experts?> GetExpertByIdAsync(int expertId)
    {
        return await _context.Experts
            .FirstOrDefaultAsync(e => e.ExpertId == expertId);
    }

    public async Task<Experts?> GetProfileByUserIdAsync(int userId)
    {
        return await _context.Experts
            .AsTracking()
            .Include(e => e.Expert) // Users navigation
            .FirstOrDefaultAsync(e => e.ExpertId == userId);
    }

    // ── Materials ───────────────────────────────────────────────────────────────

    public async Task CreateMaterialAsync(Materials material)
    {
        await _context.Materials.AddAsync(material);
    }

    public async Task<List<Materials>> GetMaterialsByExpertIdAsync(int expertId)
    {
        return await _context.Materials
            .Include(m => m.Subject)
            .Include(m => m.Grade)
            .Where(m => m.ExpertId == expertId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Materials?> GetMaterialByCodeAsync(string materialCode)
    {
        return await _context.Materials
            .Include(m => m.Subject)
            .Include(m => m.Grade)
            .FirstOrDefaultAsync(m => m.MaterialCode == materialCode);
    }

    public void UpdateMaterial(Materials material)
    {
        _context.Materials.Update(material);
    }

    public void DeleteMaterial(Materials material)
    {
        _context.Materials.Remove(material);
    }

    public async Task<Subjects?> GetSubjectByCodeAsync(string subjectCode)
    {
        return await _context.Subjects
            .FirstOrDefaultAsync(s => s.SubjectCode == subjectCode);
    }

    public async Task<Grades?> GetGradeByCodeAsync(string gradeCode)
    {
        return await _context.Grades
            .FirstOrDefaultAsync(g => g.GradeCode == gradeCode);
    }

    public async Task<int> CountPendingMaterialsAsync(int expertId)
    {
        return await _context.Materials
            .CountAsync(m => m.ExpertId == expertId && m.ApprovalStatus == 0);
    }

    public async Task<Wallets?> GetWalletByUserIdAsync(int userId)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId);
    }

    public void UpdateWallet(Wallets wallet)
    {
        _context.Wallets.Update(wallet);
    }

    public async Task CreateWalletTransactionAsync(WalletTransactions transaction)
    {
        await _context.WalletTransactions.AddAsync(transaction);
    }

    public async Task<List<MaterialSalesAnalyticsRow>> GetMaterialSalesAnalyticsByExpertAsync(
        int expertId,
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? materialCode)
    {
        var query = BuildMaterialSalesTransactionQueryByExpert(expertId, fromDate, toDate, subjectCode, gradeCode, materialCode);

        return await query
            .GroupBy(transaction => new
            {
                MaterialCode = transaction.Material != null ? transaction.Material.MaterialCode : string.Empty,
                Title = transaction.Material != null ? transaction.Material.Title : string.Empty,
                SubjectCode = transaction.Material != null && transaction.Material.Subject != null ? transaction.Material.Subject.SubjectCode : null,
                GradeCode = transaction.Material != null && transaction.Material.Grade != null ? transaction.Material.Grade.GradeCode : null
            })
            .Select(group => new MaterialSalesAnalyticsRow
            {
                MaterialCode = group.Key.MaterialCode,
                Title = group.Key.Title,
                SubjectCode = group.Key.SubjectCode,
                GradeCode = group.Key.GradeCode,
                ExpertCode = null,
                ExpertName = null,
                SoldCount = group.Count(),
                UniqueBuyerCount = group.Select(transaction => transaction.Wallet != null ? transaction.Wallet.UserId : null)
                    .Distinct()
                    .Count(userId => userId != null),
                GrossRevenue = group.Sum(transaction => Math.Abs(transaction.Amount ?? 0)),
                LastPurchasedDate = group.Max(transaction => transaction.CreatedAt)
            })
            .OrderByDescending(item => item.GrossRevenue)
            .ThenByDescending(item => item.SoldCount)
            .ToListAsync();
    }

    public async Task<RevenueForecastAnalyticsRow> GetRevenueForecastAnalyticsByExpertAsync(
        int expertId,
        DateTime currentFromDate,
        DateTime currentToDate,
        DateTime previousFromDate,
        DateTime previousToDate,
        string? subjectCode,
        string? gradeCode,
        string? materialCode)
    {
        var currentQuery = BuildMaterialSalesTransactionQueryByExpert(expertId, currentFromDate, currentToDate, subjectCode, gradeCode, materialCode);
        var previousQuery = BuildMaterialSalesTransactionQueryByExpert(expertId, previousFromDate, previousToDate, subjectCode, gradeCode, materialCode);

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

    private IQueryable<WalletTransactions> BuildMaterialSalesTransactionQueryByExpert(
        int expertId,
        DateTime? fromDate,
        DateTime? toDate,
        string? subjectCode,
        string? gradeCode,
        string? materialCode)
    {
        var query = _context.WalletTransactions
            .Include(transaction => transaction.Wallet)
            .Include(transaction => transaction.Material)
                .ThenInclude(material => material.Subject)
            .Include(transaction => transaction.Material)
                .ThenInclude(material => material.Grade)
            .Where(transaction => transaction.TransactionType == "BUY_MATERIAL"
                && transaction.Status == 1
                && transaction.Material != null
                && transaction.Material.ExpertId == expertId)
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

        if (!string.IsNullOrWhiteSpace(materialCode))
            query = query.Where(transaction => transaction.Material != null
                && transaction.Material.MaterialCode == materialCode);

        return query;
    }
}
