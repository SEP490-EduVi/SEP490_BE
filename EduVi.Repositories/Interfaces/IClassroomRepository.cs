using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IClassroomRepository
{
    /// <summary>Lấy tất cả lớp học của một giáo viên, sắp xếp mới nhất trước.</summary>
    Task<List<Classrooms>> GetClassroomsByTeacherAsync(int teacherId);

    /// <summary>Lấy chi tiết lớp học theo code (không kiểm tra owner).</summary>
    Task<Classrooms?> GetClassroomByCodeAsync(string classroomCode);

    /// <summary>Lấy lớp học theo code và kiểm tra thuộc về teacherId.</summary>
    Task<Classrooms?> GetClassroomByCodeAndTeacherAsync(string classroomCode, int teacherId);

    Task<Classrooms> CreateClassroomAsync(Classrooms classroom);

    void UpdateClassroom(Classrooms classroom);

    void DeleteClassroom(Classrooms classroom);
}
