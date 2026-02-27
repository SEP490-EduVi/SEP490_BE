using EduVi.Contracts.DTOs.Curriculum;

namespace EduVi.Services.Curriculum;

public interface ICurriculumService
{
    // ============ Subjects ============
    Task<List<SubjectResponseDto>> GetAllSubjectsAsync();
    Task<SubjectResponseDto> GetSubjectByCodeAsync(string subjectCode);
    Task<SubjectResponseDto> CreateSubjectAsync(CreateSubjectRequestDto request);
    Task<SubjectResponseDto> UpdateSubjectAsync(string subjectCode, UpdateSubjectRequestDto request);
    Task DeleteSubjectAsync(string subjectCode);

    // ============ Grades ============
    Task<List<GradeResponseDto>> GetAllGradesAsync();
    Task<GradeResponseDto> GetGradeByCodeAsync(string gradeCode);
    Task<GradeResponseDto> CreateGradeAsync(CreateGradeRequestDto request);
    Task<GradeResponseDto> UpdateGradeAsync(string gradeCode, UpdateGradeRequestDto request);
    Task DeleteGradeAsync(string gradeCode);

    // ============ Lessons ============
    Task<List<LessonResponseDto>> GetAllLessonsAsync(string? subjectCode = null);
    Task<LessonResponseDto> GetLessonByCodeAsync(string lessonCode);
    Task<LessonResponseDto> CreateLessonAsync(CreateLessonRequestDto request);
    Task<LessonResponseDto> UpdateLessonAsync(string lessonCode, UpdateLessonRequestDto request);
    Task DeleteLessonAsync(string lessonCode);
}
