using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class CurriculumRepository : ICurriculumRepository
{
    private readonly EduViContext _context;

    public CurriculumRepository(EduViContext context)
    {
        _context = context;
    }

    // ============ Subjects ============

    public async Task<List<Subjects>> GetAllSubjectsAsync()
    {
        return await _context.Subjects
            .Include(s => s.Lessons)
            .OrderBy(s => s.SubjectName)
            .ToListAsync();
    }

    public async Task<Subjects?> GetSubjectByIdAsync(int subjectId)
    {
        return await _context.Subjects
            .Include(s => s.Lessons)
            .FirstOrDefaultAsync(s => s.SubjectId == subjectId);
    }

    public async Task<Subjects?> GetSubjectByCodeAsync(string subjectCode, bool includeRelations = false)
    {
        var query = _context.Subjects.AsQueryable();
        if (includeRelations)
            query = query.Include(s => s.Lessons);
        return await query.FirstOrDefaultAsync(s => s.SubjectCode == subjectCode);
    }

    public async Task<Subjects> CreateSubjectAsync(Subjects subject)
    {
        var entry = await _context.Subjects.AddAsync(subject);
        return entry.Entity;
    }

    public void UpdateSubject(Subjects subject)
    {
        _context.Subjects.Update(subject);
    }

    public void DeleteSubject(Subjects subject)
    {
        _context.Subjects.Remove(subject);
    }

    // ============ Grades ============

    public async Task<List<Grades>> GetAllGradesAsync()
    {
        return await _context.Grades
            .OrderBy(g => g.GradeName)
            .ToListAsync();
    }

    public async Task<Grades?> GetGradeByIdAsync(int gradeId)
    {
        return await _context.Grades
            .FirstOrDefaultAsync(g => g.GradeId == gradeId);
    }

    public async Task<Grades?> GetGradeByCodeAsync(string gradeCode)
    {
        return await _context.Grades
            .FirstOrDefaultAsync(g => g.GradeCode == gradeCode);
    }

    public async Task<Grades> CreateGradeAsync(Grades grade)
    {
        var entry = await _context.Grades.AddAsync(grade);
        return entry.Entity;
    }

    public void UpdateGrade(Grades grade)
    {
        _context.Grades.Update(grade);
    }

    public void DeleteGrade(Grades grade)
    {
        _context.Grades.Remove(grade);
    }

    // ============ Lessons ============

    public async Task<List<Lessons>> GetAllLessonsAsync()
    {
        return await _context.Lessons
            .Include(l => l.Subject)
            .OrderBy(l => l.LessonCode)
            .ToListAsync();
    }

    public async Task<List<Lessons>> GetLessonsBySubjectCodeAsync(string subjectCode)
    {
        return await _context.Lessons
            .Include(l => l.Subject)
            .Where(l => l.Subject.SubjectCode == subjectCode)
            .OrderBy(l => l.LessonCode)
            .ToListAsync();
    }

    public async Task<Lessons?> GetLessonByIdAsync(int lessonId)
    {
        return await _context.Lessons
            .Include(l => l.Subject)
            .FirstOrDefaultAsync(l => l.LessonId == lessonId);
    }

    public async Task<Lessons?> GetLessonByCodeAsync(string lessonCode, bool includeRelations = false)
    {
        var query = _context.Lessons.AsQueryable();
        if (includeRelations)
            query = query.Include(l => l.Subject);
        return await query.FirstOrDefaultAsync(l => l.LessonCode == lessonCode);
    }

    public async Task<Lessons> CreateLessonAsync(Lessons lesson)
    {
        var entry = await _context.Lessons.AddAsync(lesson);
        return entry.Entity;
    }

    public void UpdateLesson(Lessons lesson)
    {
        _context.Lessons.Update(lesson);
    }

    public void DeleteLesson(Lessons lesson)
    {
        _context.Lessons.Remove(lesson);
    }
}
