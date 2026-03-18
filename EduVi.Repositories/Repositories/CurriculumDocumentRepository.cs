using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class CurriculumDocumentRepository : ICurriculumDocumentRepository
{
    private readonly EduViContext _context;

    public CurriculumDocumentRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<CurriculumDocuments?> GetByIdAsync(int curriculumDocumentId)
    {
        return await _context.CurriculumDocuments
            .FirstOrDefaultAsync(document => document.CurriculumDocumentId == curriculumDocumentId);
    }

    public async Task<CurriculumDocuments?> GetByDocumentCodeAsync(string documentCode)
    {
        return await _context.CurriculumDocuments
            .FirstOrDefaultAsync(document => document.DocumentCode == documentCode);
    }

    public async Task<List<CurriculumDocuments>> GetAllAsync()
    {
        return await _context.CurriculumDocuments
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync();
    }

    public async Task<CurriculumDocuments> CreateAsync(CurriculumDocuments document)
    {
        await _context.CurriculumDocuments.AddAsync(document);
        return document;
    }

    public void Update(CurriculumDocuments document)
    {
        _context.CurriculumDocuments.Update(document);
    }

    public async Task<bool> ExistsCompletedAsync(string subjectCode, string educationLevel, int curriculumYear)
    {
        return await _context.CurriculumDocuments
            .AnyAsync(document =>
                document.SubjectCode == subjectCode &&
                document.EducationLevel == educationLevel &&
                document.CurriculumYear == curriculumYear &&
                document.Status == 2);
    }
}
