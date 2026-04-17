using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IStudentListRepository
{
    /// <summary>Lấy tất cả lớp học của một giáo viên, sắp xếp mới nhất trước.</summary>
    Task<List<StudentLists>> GetStudentListsByTeacherAsync(int teacherId);

    /// <summary>Lấy chi tiết lớp học theo code (không kiểm tra owner).</summary>
    Task<StudentLists?> GetStudentListByCodeAsync(string studentListCode);

    /// <summary>Lấy lớp học theo code và kiểm tra thuộc về teacherId.</summary>
    Task<StudentLists?> GetStudentListByCodeAndTeacherAsync(string studentListCode, int teacherId);

    Task<StudentLists> CreateStudentListAsync(StudentLists studentList);

    void UpdateStudentList(StudentLists studentList);

    void DeleteStudentList(StudentLists studentList);
}
