using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class TeacherRepository : ITeacherRepository
{
    private readonly EduViContext _context;

    public TeacherRepository(EduViContext context)
    {
        _context = context;
    }

    // ── Browse Materials ───────────────────────────────────────────────────────

    public async Task<List<Materials>> GetApprovedMaterialsAsync(
        string? subjectCode, string? gradeCode, string? type, string? keyword)
    {
        var query = _context.Materials
            .Include(m => m.Expert)
                .ThenInclude(e => e.Expert) // Users navigation
            .Include(m => m.Subject)
            .Include(m => m.Grade)
            .Where(m => m.ApprovalStatus == 1)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(subjectCode))
            query = query.Where(m => m.Subject != null && m.Subject.SubjectCode == subjectCode);

        if (!string.IsNullOrWhiteSpace(gradeCode))
            query = query.Where(m => m.Grade != null && m.Grade.GradeCode == gradeCode);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(m => m.Type == type);

        if (!string.IsNullOrWhiteSpace(keyword))
            query = query.Where(m => m.Title.Contains(keyword) || (m.Description != null && m.Description.Contains(keyword)));

        return await query
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync();
    }

    public async Task<Materials?> GetApprovedMaterialByCodeAsync(string materialCode)
    {
        return await _context.Materials
            .Include(m => m.Expert)
                .ThenInclude(e => e.Expert) // Users navigation
            .Include(m => m.Subject)
            .Include(m => m.Grade)
            .FirstOrDefaultAsync(m => m.MaterialCode == materialCode && m.ApprovalStatus == 1);
    }

    // ── Purchase ───────────────────────────────────────────────────────────────

    public async Task<bool> HasTeacherPurchasedAsync(int teacherId, int materialId)
    {
        return await _context.TeacherMaterials
            .AnyAsync(tm => tm.TeacherId == teacherId && tm.MaterialId == materialId);
    }

    public async Task CreateTeacherMaterialAsync(TeacherMaterials teacherMaterial)
    {
        await _context.TeacherMaterials.AddAsync(teacherMaterial);
    }

    public async Task<List<TeacherMaterials>> GetPurchasedMaterialsAsync(int teacherId)
    {
        return await _context.TeacherMaterials
            .Include(tm => tm.Material)
                .ThenInclude(m => m.Expert)
                    .ThenInclude(e => e.Expert) // Users navigation
            .Include(tm => tm.Material)
                .ThenInclude(m => m.Subject)
            .Include(tm => tm.Material)
                .ThenInclude(m => m.Grade)
            .Where(tm => tm.TeacherId == teacherId)
            .OrderByDescending(tm => tm.PurchasedDate)
            .ToListAsync();
    }

    // ── Wallet ─────────────────────────────────────────────────────────────────

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
}
