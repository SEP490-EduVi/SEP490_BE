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
}
