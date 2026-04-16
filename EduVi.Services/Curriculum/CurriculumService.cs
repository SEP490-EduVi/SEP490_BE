using EduVi.Contracts.DTOs.Curriculum;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Curriculum;

public class CurriculumService : ICurriculumService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CurriculumService> _logger;

    public CurriculumService(IUnitOfWork unitOfWork, ILogger<CurriculumService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    #region Subjects

    public async Task<List<SubjectResponseDto>> GetAllSubjectsAsync()
    {
        var subjects = await _unitOfWork.CurriculumRepository.GetAllSubjectsAsync();
        return subjects.Select(MapToSubjectResponse).ToList();
    }

    public async Task<SubjectResponseDto> GetSubjectByCodeAsync(string subjectCode)
    {
        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(subjectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Môn học '{subjectCode}' không tồn tại");

        return MapToSubjectResponse(subject);
    }

    public async Task<SubjectResponseDto> CreateSubjectAsync(CreateSubjectRequestDto request)
    {
        var existing = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode);
        if (existing is not null)
            throw new InvalidOperationException($"Mã môn học '{request.SubjectCode}' đã tồn tại");

        var subject = new Subjects
        {
            SubjectCode = request.SubjectCode,
            SubjectName = request.SubjectName
        };

        await _unitOfWork.CurriculumRepository.CreateSubjectAsync(subject);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Subject: {SubjectCode}", subject.SubjectCode);

        var saved = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(subject.SubjectCode, includeRelations: true);
        return MapToSubjectResponse(saved!);
    }

    public async Task<SubjectResponseDto> UpdateSubjectAsync(string subjectCode, UpdateSubjectRequestDto request)
    {
        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(subjectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Môn học '{subjectCode}' không tồn tại");

        if (request.SubjectCode is not null && request.SubjectCode != subjectCode)
        {
            var existing = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode);
            if (existing is not null)
                throw new InvalidOperationException($"Mã môn học '{request.SubjectCode}' đã được sử dụng");
            subject.SubjectCode = request.SubjectCode;
        }

        if (request.SubjectName is not null)
            subject.SubjectName = request.SubjectName;

        _unitOfWork.CurriculumRepository.UpdateSubject(subject);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated Subject: {SubjectCode}", subject.SubjectCode);

        var saved = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(subject.SubjectCode, includeRelations: true);
        return MapToSubjectResponse(saved!);
    }

    public async Task DeleteSubjectAsync(string subjectCode)
    {
        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(subjectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Môn học '{subjectCode}' không tồn tại");

        if (subject.Lessons.Count > 0)
            throw new InvalidOperationException($"Không thể xóa môn học đang có {subject.Lessons.Count} bài học");

        _unitOfWork.CurriculumRepository.DeleteSubject(subject);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted Subject: {SubjectCode}", subjectCode);
    }

    #endregion

    #region Grades

    public async Task<List<GradeResponseDto>> GetAllGradesAsync()
    {
        var grades = await _unitOfWork.CurriculumRepository.GetAllGradesAsync();
        return grades.Select(MapToGradeResponse).ToList();
    }

    public async Task<GradeResponseDto> GetGradeByCodeAsync(string gradeCode)
    {
        var grade = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(gradeCode)
            ?? throw new KeyNotFoundException($"Khối lớp '{gradeCode}' không tồn tại");

        return MapToGradeResponse(grade);
    }

    public async Task<GradeResponseDto> CreateGradeAsync(CreateGradeRequestDto request)
    {
        var existing = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(request.GradeCode);
        if (existing is not null)
            throw new InvalidOperationException($"Mã khối lớp '{request.GradeCode}' đã tồn tại");

        var grade = new Grades
        {
            GradeCode = request.GradeCode,
            GradeName = request.GradeName
        };

        await _unitOfWork.CurriculumRepository.CreateGradeAsync(grade);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Grade: {GradeCode}", grade.GradeCode);
        return MapToGradeResponse(grade);
    }

    public async Task<GradeResponseDto> UpdateGradeAsync(string gradeCode, UpdateGradeRequestDto request)
    {
        var grade = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(gradeCode)
            ?? throw new KeyNotFoundException($"Khối lớp '{gradeCode}' không tồn tại");

        if (request.GradeCode is not null && request.GradeCode != gradeCode)
        {
            var existing = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(request.GradeCode);
            if (existing is not null)
                throw new InvalidOperationException($"Mã khối lớp '{request.GradeCode}' đã được sử dụng");
            grade.GradeCode = request.GradeCode;
        }

        if (request.GradeName is not null)
            grade.GradeName = request.GradeName;

        _unitOfWork.CurriculumRepository.UpdateGrade(grade);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated Grade: {GradeCode}", grade.GradeCode);
        return MapToGradeResponse(grade);
    }

    public async Task DeleteGradeAsync(string gradeCode)
    {
        var grade = await _unitOfWork.CurriculumRepository.GetGradeByCodeAsync(gradeCode)
            ?? throw new KeyNotFoundException($"Khối lớp '{gradeCode}' không tồn tại");

        _unitOfWork.CurriculumRepository.DeleteGrade(grade);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted Grade: {GradeCode}", gradeCode);
    }

    #endregion

    #region Lessons

    public async Task<List<LessonResponseDto>> GetAllLessonsAsync(string? subjectCode = null)
    {
        var lessons = subjectCode is not null
            ? await _unitOfWork.CurriculumRepository.GetLessonsBySubjectCodeAsync(subjectCode)
            : await _unitOfWork.CurriculumRepository.GetAllLessonsAsync();

        return lessons.Select(MapToLessonResponse).ToList();
    }

    public async Task<LessonResponseDto> GetLessonByCodeAsync(string lessonCode)
    {
        var lesson = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(lessonCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Bài học '{lessonCode}' không tồn tại");

        return MapToLessonResponse(lesson);
    }

    public async Task<LessonResponseDto> CreateLessonAsync(CreateLessonRequestDto request)
    {
        var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode)
            ?? throw new KeyNotFoundException($"Môn học '{request.SubjectCode}' không tồn tại");

        var existing = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(request.LessonCode);
        if (existing is not null)
            throw new InvalidOperationException($"Mã bài học '{request.LessonCode}' đã tồn tại");

        var lesson = new Lessons
        {
            LessonCode = request.LessonCode,
            LessonName = request.LessonName,
            SubjectId = subject.SubjectId
        };

        await _unitOfWork.CurriculumRepository.CreateLessonAsync(lesson);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Created Lesson: {LessonCode} for Subject {SubjectCode}",
            lesson.LessonCode, request.SubjectCode);

        var saved = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(lesson.LessonCode, includeRelations: true);
        return MapToLessonResponse(saved!);
    }

    public async Task<LessonResponseDto> UpdateLessonAsync(string lessonCode, UpdateLessonRequestDto request)
    {
        var lesson = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(lessonCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Bài học '{lessonCode}' không tồn tại");

        if (request.LessonCode is not null && request.LessonCode != lessonCode)
        {
            var existing = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(request.LessonCode);
            if (existing is not null)
                throw new InvalidOperationException($"Mã bài học '{request.LessonCode}' đã được sử dụng");
            lesson.LessonCode = request.LessonCode;
        }

        if (request.LessonName is not null)
            lesson.LessonName = request.LessonName;

        if (request.SubjectCode is not null)
        {
            var subject = await _unitOfWork.CurriculumRepository.GetSubjectByCodeAsync(request.SubjectCode)
                ?? throw new KeyNotFoundException($"Môn học '{request.SubjectCode}' không tồn tại");
            lesson.SubjectId = subject.SubjectId;
        }

        _unitOfWork.CurriculumRepository.UpdateLesson(lesson);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Updated Lesson: {LessonCode}", lesson.LessonCode);

        var saved = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(lesson.LessonCode, includeRelations: true);
        return MapToLessonResponse(saved!);
    }

    public async Task DeleteLessonAsync(string lessonCode)
    {
        var lesson = await _unitOfWork.CurriculumRepository.GetLessonByCodeAsync(lessonCode)
            ?? throw new KeyNotFoundException($"Bài học '{lessonCode}' không tồn tại");

        _unitOfWork.CurriculumRepository.DeleteLesson(lesson);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Deleted Lesson: {LessonCode}", lessonCode);
    }

    #endregion

    #region Mapping Helpers

    private static SubjectResponseDto MapToSubjectResponse(Subjects s) => new()
    {
        SubjectCode = s.SubjectCode,
        SubjectName = s.SubjectName,
        LessonCount = s.Lessons?.Count ?? 0
    };

    private static GradeResponseDto MapToGradeResponse(Grades g) => new()
    {
        GradeCode = g.GradeCode,
        GradeName = g.GradeName
    };

    private static LessonResponseDto MapToLessonResponse(Lessons l) => new()
    {
        LessonCode = l.LessonCode,
        LessonName = l.LessonName,
        SubjectCode = l.Subject?.SubjectCode,
        SubjectName = l.Subject?.SubjectName
    };

    #endregion
}
