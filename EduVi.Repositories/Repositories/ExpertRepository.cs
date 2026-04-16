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
}
