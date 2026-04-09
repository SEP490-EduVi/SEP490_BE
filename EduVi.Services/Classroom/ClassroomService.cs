using EduVi.Contracts.DTOs.Classroom;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using System.Text.Json;

namespace EduVi.Services.Classroom;

public class ClassroomService : IClassroomService
{
    private readonly IUnitOfWork _unitOfWork;

    public ClassroomService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ClassroomResponseDto> CreateClassroomAsync(int teacherId, CreateClassroomRequest request)
    {
        var teacher = await GetTeacherAsync(teacherId);

        var classroomCode = $"cls_{teacher.TeacherId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var classroom = new Classrooms
        {
            ClassroomCode = classroomCode,
            TeacherId = teacher.TeacherId,
            Name = request.Name.Trim(),
            GradeLabel = request.GradeLabel?.Trim(),
            SchoolYear = request.SchoolYear?.Trim(),
            Students = null,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.ClassroomRepository.CreateClassroomAsync(classroom);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(classroom);
    }

    public async Task<List<ClassroomResponseDto>> GetClassroomsAsync(int teacherId)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var classrooms = await _unitOfWork.ClassroomRepository.GetClassroomsByTeacherAsync(teacher.TeacherId);
        return classrooms.Select(MapToDto).ToList();
    }

    public async Task<ClassroomResponseDto> GetClassroomAsync(int teacherId, string classroomCode)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var classroom = await _unitOfWork.ClassroomRepository
            .GetClassroomByCodeAndTeacherAsync(classroomCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Lớp học '{classroomCode}' không tồn tại hoặc không thuộc về bạn");

        return MapToDto(classroom);
    }

    public async Task<ClassroomResponseDto> ImportStudentsAsync(int teacherId, string classroomCode, ImportStudentsRequest request)
    {
        if (request.Students.Count == 0)
            throw new InvalidOperationException("Danh sách học sinh không được để trống");

        var cleanedStudents = request.Students
            .Select(name => name?.Trim() ?? string.Empty)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct()
            .ToList();

        if (cleanedStudents.Count == 0)
            throw new InvalidOperationException("Danh sách học sinh không hợp lệ sau khi lọc");

        var teacher = await GetTeacherAsync(teacherId);
        var classroom = await _unitOfWork.ClassroomRepository
            .GetClassroomByCodeAndTeacherAsync(classroomCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Lớp học '{classroomCode}' không tồn tại hoặc không thuộc về bạn");

        classroom.Students = JsonSerializer.Serialize(cleanedStudents);
        classroom.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ClassroomRepository.UpdateClassroom(classroom);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(classroom);
    }

    public async Task<ClassroomResponseDto> UpdateClassroomAsync(int teacherId, string classroomCode, UpdateClassroomRequest request)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var classroom = await _unitOfWork.ClassroomRepository
            .GetClassroomByCodeAndTeacherAsync(classroomCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Lớp học '{classroomCode}' không tồn tại hoặc không thuộc về bạn");

        if (request.Name is not null)
            classroom.Name = request.Name.Trim();

        if (request.GradeLabel is not null)
            classroom.GradeLabel = request.GradeLabel.Trim();

        if (request.SchoolYear is not null)
            classroom.SchoolYear = request.SchoolYear.Trim();

        classroom.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ClassroomRepository.UpdateClassroom(classroom);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(classroom);
    }

    public async Task DeleteClassroomAsync(int teacherId, string classroomCode)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var classroom = await _unitOfWork.ClassroomRepository
            .GetClassroomByCodeAndTeacherAsync(classroomCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Lớp học '{classroomCode}' không tồn tại hoặc không thuộc về bạn");

        _unitOfWork.ClassroomRepository.DeleteClassroom(classroom);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task<Teachers> GetTeacherAsync(int userId)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng");

        return user.Teachers
            ?? throw new InvalidOperationException("Chỉ giáo viên mới có thể quản lý lớp học");
    }

    private static ClassroomResponseDto MapToDto(Classrooms classroom)
    {
        var students = DeserializeStudents(classroom.Students);

        return new ClassroomResponseDto
        {
            ClassroomCode = classroom.ClassroomCode,
            Name = classroom.Name,
            GradeLabel = classroom.GradeLabel,
            SchoolYear = classroom.SchoolYear,
            Students = students,
            StudentCount = students.Count,
            CreatedAt = classroom.CreatedAt,
            UpdatedAt = classroom.UpdatedAt
        };
    }

    private static List<string> DeserializeStudents(string? studentsJson)
    {
        if (string.IsNullOrWhiteSpace(studentsJson))
            return new List<string>();

        try
        {
            return JsonSerializer.Deserialize<List<string>>(studentsJson) ?? new List<string>();
        }
        catch (JsonException)
        {
            return new List<string>();
        }
    }
}
