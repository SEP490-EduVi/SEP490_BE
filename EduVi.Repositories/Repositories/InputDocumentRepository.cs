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
            .FirstOrDefaultAsync(document => document.DocumentCode == documentCode && document.TeacherId == teacherId);
    }

    public async Task<InputDocuments?> GetExistingInputDocumentAsync(int teacherId, int projectId, int subjectId, int gradeId, int? lessonId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .FirstOrDefaultAsync(document =>
                document.TeacherId == teacherId &&
                document.ProjectId == projectId &&
                document.SubjectId == subjectId &&
                document.GradeId == gradeId &&
                document.LessonId == lessonId);
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
        _context.InputDocuments.Remove(document);
    }

    public async Task<List<InputDocuments>> GetInputDocumentsByTeacherAsync(int teacherId)
    {
        return await _context.InputDocuments
            .Include(document => document.Subject)
            .Include(document => document.Grade)
            .Include(document => document.Lesson)
            .Include(document => document.Project)
            .Where(document => document.TeacherId == teacherId)
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
            .Where(document => document.TeacherId == teacherId && document.ProjectId == projectId)
            .OrderByDescending(document => document.UploadDate)
            .ToListAsync();
    }
}
