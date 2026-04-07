using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class InputDocumentRepository : IInputDocumentRepository
{
    private readonly EduViContext _context;

    public InputDocumentRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<InputDocuments?> GetInputDocumentByIdAsync(int documentId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .FirstOrDefaultAsync(document => document.DocumentId == documentId);
    }

    public async Task<InputDocuments?> GetInputDocumentByCodeAsync(string documentCode)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .FirstOrDefaultAsync(document => document.DocumentCode == documentCode);
    }

    public async Task<InputDocuments?> GetInputDocumentByCodeAndTeacherAsync(string documentCode, int teacherId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .FirstOrDefaultAsync(document => document.DocumentCode == documentCode
                                          && document.TeacherId == teacherId
                                          && document.Status != 1); // exclude Deleted
    }

    public async Task<InputDocuments> CreateInputDocumentAsync(InputDocuments document)
    {
        var entry = await _context.InputDocuments.AddAsync(document);
        return entry.Entity;
    }

    public void UpdateInputDocument(InputDocuments document)
    {
        _context.InputDocuments.Update(document);
    }

    public void DeleteInputDocument(InputDocuments document)
    {
        document.Status = 1; // Deleted (soft delete)
        _context.InputDocuments.Update(document);
    }

    public async Task<List<InputDocuments>> GetInputDocumentsByTeacherAsync(int teacherId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .Where(document => document.TeacherId == teacherId && document.Status != 1) // exclude Deleted
            .OrderByDescending(document => document.UploadDate)
            .ToListAsync();
    }

    public async Task<List<InputDocuments>> GetInputDocumentsByTeacherAndProjectAsync(int teacherId, int projectId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .Where(document => document.TeacherId == teacherId && document.ProjectId == projectId && document.Status != 1) // exclude Deleted
            .OrderByDescending(document => document.UploadDate)
            .ToListAsync();
    }
}
