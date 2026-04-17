using EduVi.Contracts.DTOs.StudentLists;

namespace EduVi.Services.StudentLists;

public interface IStudentListService
{
    /// <summary>Tạo danh sách học sinh mới. StudentListCode được sinh tự động.</summary>
    Task<StudentListResponseDto> CreateStudentListAsync(int teacherId, CreateStudentListRequest request);

    /// <summary>Lấy danh sách tất cả student list của giáo viên.</summary>
    Task<List<StudentListResponseDto>> GetStudentListsAsync(int teacherId);

    /// <summary>Lấy chi tiết một student list theo code (kiểm tra ownership).</summary>
    Task<StudentListResponseDto> GetStudentListAsync(int teacherId, string studentListCode);

    /// <summary>
    /// Import (ghi đè) danh sách học sinh cho student list.
    /// FE xử lý Excel rồi gửi xuống dưới dạng mảng tên string.
    /// </summary>
    Task<StudentListResponseDto> ImportStudentsAsync(int teacherId, string studentListCode, ImportStudentsRequest request);

    /// <summary>Cập nhật thông tin student list.</summary>
    Task<StudentListResponseDto> UpdateStudentListAsync(int teacherId, string studentListCode, UpdateStudentListRequest request);

    /// <summary>Xóa student list.</summary>
    Task DeleteStudentListAsync(int teacherId, string studentListCode);
}
