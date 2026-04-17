using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.StudentLists;
using EduVi.Services.StudentLists;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/teacher/student-lists")]
[Authorize(Roles = "Teacher")]
public class StudentListController : ControllerBase
{
    private readonly IStudentListService _studentListService;
    private readonly ILogger<StudentListController> _logger;

    public StudentListController(IStudentListService studentListService, ILogger<StudentListController> logger)
    {
        _studentListService = studentListService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo student list mới. StudentListCode được sinh tự động.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<StudentListResponseDto>>> CreateStudentList(
        [FromBody] CreateStudentListRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _studentListService.CreateStudentListAsync(userId, request);
            return Ok(ApiResponse<StudentListResponseDto>.Success(result, "Danh sách học sinh đã được tạo thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<StudentListResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating student list for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<StudentListResponseDto>.Fail("Đã xảy ra lỗi khi tạo danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả student list của giáo viên đang đăng nhập.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<StudentListResponseDto>>>> GetStudentLists()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _studentListService.GetStudentListsAsync(userId);
            return Ok(ApiResponse<List<StudentListResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting student lists for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<List<StudentListResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Lấy chi tiết một student list (bao gồm danh sách học sinh).
    /// </summary>
    [HttpGet("{studentListCode}")]
    public async Task<ActionResult<ApiResponse<StudentListResponseDto>>> GetStudentList(string studentListCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _studentListService.GetStudentListAsync(userId, studentListCode);
            return Ok(ApiResponse<StudentListResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<StudentListResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting student list {StudentListCode}", studentListCode);
            return StatusCode(500, ApiResponse<StudentListResponseDto>.Fail("Đã xảy ra lỗi khi lấy thông tin danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin student list.
    /// </summary>
    [HttpPut("{studentListCode}")]
    public async Task<ActionResult<ApiResponse<StudentListResponseDto>>> UpdateStudentList(
        string studentListCode, [FromBody] UpdateStudentListRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _studentListService.UpdateStudentListAsync(userId, studentListCode, request);
            return Ok(ApiResponse<StudentListResponseDto>.Success(result, "Cập nhật danh sách học sinh thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<StudentListResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating student list {StudentListCode}", studentListCode);
            return StatusCode(500, ApiResponse<StudentListResponseDto>.Fail("Đã xảy ra lỗi khi cập nhật danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Import (ghi đè) danh sách học sinh cho student list.
    /// FE xử lý file Excel rồi gửi danh sách tên xuống API này.
    /// </summary>
    [HttpPost("{studentListCode}/students")]
    public async Task<ActionResult<ApiResponse<StudentListResponseDto>>> ImportStudents(
        string studentListCode, [FromBody] ImportStudentsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _studentListService.ImportStudentsAsync(userId, studentListCode, request);
            return Ok(ApiResponse<StudentListResponseDto>.Success(result, $"Đã nhập {result.StudentCount} học sinh thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<StudentListResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<StudentListResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing students for student list {StudentListCode}", studentListCode);
            return StatusCode(500, ApiResponse<StudentListResponseDto>.Fail("Đã xảy ra lỗi khi nhập danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Xóa student list.
    /// </summary>
    [HttpDelete("{studentListCode}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteStudentList(string studentListCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _studentListService.DeleteStudentListAsync(userId, studentListCode);
            return Ok(ApiResponse<object>.Success(null, "Danh sách học sinh đã được xóa"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting student list {StudentListCode}", studentListCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xóa danh sách học sinh", 500));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy ID người dùng trong token");
        return userId;
    }
}
