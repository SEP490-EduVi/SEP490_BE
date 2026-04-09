using EduVi.Contracts.DTOs.Classroom;

namespace EduVi.Services.Classroom;

public interface IClassroomService
{
    /// <summary>Tạo lớp học mới. ClassroomCode được sinh tự động.</summary>
    Task<ClassroomResponseDto> CreateClassroomAsync(int teacherId, CreateClassroomRequest request);

    /// <summary>Lấy danh sách tất cả lớp học của giáo viên.</summary>
    Task<List<ClassroomResponseDto>> GetClassroomsAsync(int teacherId);

    /// <summary>Lấy chi tiết một lớp học theo code (kiểm tra ownership).</summary>
    Task<ClassroomResponseDto> GetClassroomAsync(int teacherId, string classroomCode);

    /// <summary>
    /// Import (ghi đè) danh sách học sinh cho lớp.
    /// FE xử lý Excel rồi gửi xuống dưới dạng mảng tên string.
    /// </summary>
    Task<ClassroomResponseDto> ImportStudentsAsync(int teacherId, string classroomCode, ImportStudentsRequest request);

    /// <summary>Cập nhật thông tin lớp (Name, GradeLabel, SchoolYear).</summary>
    Task<ClassroomResponseDto> UpdateClassroomAsync(int teacherId, string classroomCode, UpdateClassroomRequest request);

    /// <summary>Xóa lớp học.</summary>
    Task DeleteClassroomAsync(int teacherId, string classroomCode);
}
