using EduVi.Contracts.DTOs.StudentLists;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using System.Text.Json;
using StudentListEntity = EduVi.Repositories.Models.StudentLists;

namespace EduVi.Services.StudentLists;

public class StudentListService : IStudentListService
{
    private readonly IUnitOfWork _unitOfWork;

    public StudentListService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<StudentListResponseDto> CreateStudentListAsync(int teacherId, CreateStudentListRequest request)
    {
        var teacher = await GetTeacherAsync(teacherId);

        var studentListCode = $"sls_{teacher.TeacherId}_{DateTime.UtcNow:yyyyMMddHHmmss}";

        var studentList = new StudentListEntity
        {
            StudentListCode = studentListCode,
            TeacherId = teacher.TeacherId,
            Description = request.Description.Trim(),
            Students = null,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.StudentListRepository.CreateStudentListAsync(studentList);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(studentList);
    }

    public async Task<List<StudentListResponseDto>> GetStudentListsAsync(int teacherId)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var studentLists = await _unitOfWork.StudentListRepository.GetStudentListsByTeacherAsync(teacher.TeacherId);
        return studentLists.Select(MapToDto).ToList();
    }

    public async Task<StudentListResponseDto> GetStudentListAsync(int teacherId, string studentListCode)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var studentList = await _unitOfWork.StudentListRepository
            .GetStudentListByCodeAndTeacherAsync(studentListCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Danh sách học sinh '{studentListCode}' không tồn tại hoặc không thuộc về bạn");

        return MapToDto(studentList);
    }

    public async Task<StudentListResponseDto> ImportStudentsAsync(int teacherId, string studentListCode, ImportStudentsRequest request)
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
        var studentList = await _unitOfWork.StudentListRepository
            .GetStudentListByCodeAndTeacherAsync(studentListCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Danh sách học sinh '{studentListCode}' không tồn tại hoặc không thuộc về bạn");

        studentList.Students = JsonSerializer.Serialize(cleanedStudents);
        studentList.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.StudentListRepository.UpdateStudentList(studentList);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(studentList);
    }

    public async Task<StudentListResponseDto> UpdateStudentListAsync(int teacherId, string studentListCode, UpdateStudentListRequest request)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var studentList = await _unitOfWork.StudentListRepository
            .GetStudentListByCodeAndTeacherAsync(studentListCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Danh sách học sinh '{studentListCode}' không tồn tại hoặc không thuộc về bạn");

        if (request.Description is not null)
            studentList.Description = request.Description.Trim();

        studentList.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.StudentListRepository.UpdateStudentList(studentList);
        await _unitOfWork.SaveChangesAsync();

        return MapToDto(studentList);
    }

    public async Task DeleteStudentListAsync(int teacherId, string studentListCode)
    {
        var teacher = await GetTeacherAsync(teacherId);
        var studentList = await _unitOfWork.StudentListRepository
            .GetStudentListByCodeAndTeacherAsync(studentListCode, teacher.TeacherId)
            ?? throw new KeyNotFoundException($"Danh sách học sinh '{studentListCode}' không tồn tại hoặc không thuộc về bạn");

        _unitOfWork.StudentListRepository.DeleteStudentList(studentList);
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

    private static StudentListResponseDto MapToDto(StudentListEntity studentList)
    {
        var students = DeserializeStudents(studentList.Students);

        return new StudentListResponseDto
        {
            StudentListId = studentList.StudentListId,
            StudentListCode = studentList.StudentListCode,
            TeacherId = studentList.TeacherId,
            Description = studentList.Description,
            Students = students,
            StudentCount = students.Count,
            CreatedAt = studentList.CreatedAt,
            UpdatedAt = studentList.UpdatedAt
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
