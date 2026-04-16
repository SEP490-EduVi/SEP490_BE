using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Classroom;
using EduVi.Services.Classroom;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/teacher/classrooms")]
[Authorize(Roles = "Teacher")]
public class ClassroomController : ControllerBase
{
    private readonly IClassroomService _classroomService;
    private readonly ILogger<ClassroomController> _logger;

    public ClassroomController(IClassroomService classroomService, ILogger<ClassroomController> logger)
    {
        _classroomService = classroomService;
        _logger = logger;
    }

    /// <summary>
    /// Tạo lớp học mới. ClassroomCode được sinh tự động.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<ClassroomResponseDto>>> CreateClassroom(
        [FromBody] CreateClassroomRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _classroomService.CreateClassroomAsync(userId, request);
            return Ok(ApiResponse<ClassroomResponseDto>.Success(result, "Lớp học đã được tạo thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ClassroomResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating classroom for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<ClassroomResponseDto>.Fail("Đã xảy ra lỗi khi tạo lớp học", 500));
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả lớp học của giáo viên đang đăng nhập.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ClassroomResponseDto>>>> GetClassrooms()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _classroomService.GetClassroomsAsync(userId);
            return Ok(ApiResponse<List<ClassroomResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting classrooms for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<List<ClassroomResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách lớp học", 500));
        }
    }

    /// <summary>
    /// Lấy chi tiết một lớp học (bao gồm danh sách học sinh).
    /// </summary>
    [HttpGet("{classroomCode}")]
    public async Task<ActionResult<ApiResponse<ClassroomResponseDto>>> GetClassroom(string classroomCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _classroomService.GetClassroomAsync(userId, classroomCode);
            return Ok(ApiResponse<ClassroomResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ClassroomResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting classroom {ClassroomCode}", classroomCode);
            return StatusCode(500, ApiResponse<ClassroomResponseDto>.Fail("Đã xảy ra lỗi khi lấy thông tin lớp học", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin lớp học (tên, gradeLabel, schoolYear).
    /// </summary>
    [HttpPut("{classroomCode}")]
    public async Task<ActionResult<ApiResponse<ClassroomResponseDto>>> UpdateClassroom(
        string classroomCode, [FromBody] UpdateClassroomRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _classroomService.UpdateClassroomAsync(userId, classroomCode, request);
            return Ok(ApiResponse<ClassroomResponseDto>.Success(result, "Cập nhật lớp học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ClassroomResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating classroom {ClassroomCode}", classroomCode);
            return StatusCode(500, ApiResponse<ClassroomResponseDto>.Fail("Đã xảy ra lỗi khi cập nhật lớp học", 500));
        }
    }

    /// <summary>
    /// Import (ghi đè) danh sách học sinh cho lớp.
    /// FE xử lý file Excel rồi gửi danh sách tên xuống API này.
    /// </summary>
    [HttpPost("{classroomCode}/students")]
    public async Task<ActionResult<ApiResponse<ClassroomResponseDto>>> ImportStudents(
        string classroomCode, [FromBody] ImportStudentsRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _classroomService.ImportStudentsAsync(userId, classroomCode, request);
            return Ok(ApiResponse<ClassroomResponseDto>.Success(result, $"Đã nhập {result.StudentCount} học sinh thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ClassroomResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ClassroomResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error importing students for classroom {ClassroomCode}", classroomCode);
            return StatusCode(500, ApiResponse<ClassroomResponseDto>.Fail("Đã xảy ra lỗi khi nhập danh sách học sinh", 500));
        }
    }

    /// <summary>
    /// Xóa lớp học.
    /// </summary>
    [HttpDelete("{classroomCode}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteClassroom(string classroomCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _classroomService.DeleteClassroomAsync(userId, classroomCode);
            return Ok(ApiResponse<object>.Success(null, "Lớp học đã được xóa"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting classroom {ClassroomCode}", classroomCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xóa lớp học", 500));
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
