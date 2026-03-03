using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class StaffRepository : IStaffRepository
{
    private readonly EduViContext _context;

    public StaffRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<List<ExpertVerifications>> GetPendingVerificationsAsync()
    {
        return await _context.ExpertVerifications
            .Include(v => v.Expert)
                .ThenInclude(e => e.Expert) // Users navigation
            .Where(v => v.Status == "pending")
            .OrderBy(v => v.UploadedAt)
            .ToListAsync();
    }

    public async Task<ExpertVerifications?> GetVerificationByCodeAsync(string verificationCode)
    {
        return await _context.ExpertVerifications
            .Include(v => v.Expert)
                .ThenInclude(e => e.Expert) // Users navigation
            .FirstOrDefaultAsync(v => v.VerificationCode == verificationCode);
    }

    public void UpdateVerification(ExpertVerifications verification)
    {
        _context.ExpertVerifications.Update(verification);
    }

    public async Task<Experts?> GetExpertByIdAsync(int expertId)
    {
        return await _context.Experts
            .Include(e => e.Expert) // Users navigation
            .FirstOrDefaultAsync(e => e.ExpertId == expertId);
    }

    public void UpdateExpert(Experts expert)
    {
        _context.Experts.Update(expert);
    }

    public async Task<bool> HasOtherApprovedVerificationAsync(int expertId, string excludeVerificationCode)
    {
        return await _context.ExpertVerifications
            .AnyAsync(v => v.ExpertId == expertId
                        && v.VerificationCode != excludeVerificationCode
                        && v.Status == "approved");
    }
}
