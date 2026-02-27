using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface ICurriculumRepository
{
    // ============ Subjects ============
    Task<List<Subjects>> GetAllSubjectsAsync();
    Task<Subjects?> GetSubjectByIdAsync(int subjectId);
    Task<Subjects?> GetSubjectByCodeAsync(string subjectCode, bool includeRelations = false);
    Task<Subjects> CreateSubjectAsync(Subjects subject);
    void UpdateSubject(Subjects subject);
    void DeleteSubject(Subjects subject);

    // ============ Grades ============
    Task<List<Grades>> GetAllGradesAsync();
    Task<Grades?> GetGradeByIdAsync(int gradeId);
    Task<Grades?> GetGradeByCodeAsync(string gradeCode);
    Task<Grades> CreateGradeAsync(Grades grade);
    void UpdateGrade(Grades grade);
    void DeleteGrade(Grades grade);

    // ============ Lessons ============
    Task<List<Lessons>> GetAllLessonsAsync();
    Task<List<Lessons>> GetLessonsBySubjectCodeAsync(string subjectCode);
    Task<Lessons?> GetLessonByIdAsync(int lessonId);
    Task<Lessons?> GetLessonByCodeAsync(string lessonCode, bool includeRelations = false);
    Task<Lessons> CreateLessonAsync(Lessons lesson);
    void UpdateLesson(Lessons lesson);
    void DeleteLesson(Lessons lesson);
}
