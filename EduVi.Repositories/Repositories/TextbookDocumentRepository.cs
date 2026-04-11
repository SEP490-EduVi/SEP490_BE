using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class TextbookDocumentRepository : ITextbookDocumentRepository
{
    private readonly EduViContext _context;

    public TextbookDocumentRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<TextbookDocuments?> GetByIdAsync(int textbookDocumentId)
    {
        return await _context.TextbookDocuments
            .FirstOrDefaultAsync(document => document.TextbookDocumentId == textbookDocumentId);
    }

    public async Task<TextbookDocuments?> GetByDocumentCodeAsync(string documentCode)
    {
        return await _context.TextbookDocuments
            .FirstOrDefaultAsync(document => document.DocumentCode == documentCode);
    }

    public async Task<List<TextbookDocuments>> GetAllAsync()
    {
        return await _context.TextbookDocuments
            .OrderByDescending(document => document.CreatedAt)
            .ToListAsync();
    }

    public async Task<TextbookDocuments> CreateAsync(TextbookDocuments document)
    {
        await _context.TextbookDocuments.AddAsync(document);
        return document;
    }

    public void Update(TextbookDocuments document)
    {
        _context.TextbookDocuments.Update(document);
    }

    public async Task<bool> ExistsCompletedAsync(string subjectCode, string gradeCode)
    {
        return await _context.TextbookDocuments
            .AnyAsync(document =>
                document.SubjectCode == subjectCode &&
                document.GradeCode == gradeCode &&
                document.Status == 2);
    }
}
