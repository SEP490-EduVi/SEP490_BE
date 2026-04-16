using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Authentication.Request;
using EduVi.Contracts.DTOs.Profile;
using EduVi.Services.Authentication;
using EduVi.Services.Teacher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/teacher")]
[Authorize(Roles = "Teacher")]
public class TeacherController : ControllerBase
{
    private readonly ITeacherService _teacherService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<TeacherController> _logger;

    public TeacherController(
        ITeacherService teacherService,
        IAuthenticationService authenticationService,
        ILogger<TeacherController> logger)
    {
        _teacherService = teacherService;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Xem thông tin profile của Teacher đang đăng nhập.
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<TeacherProfileResponse>>> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _teacherService.GetProfileAsync(userId);
            return Ok(ApiResponse<TeacherProfileResponse>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<TeacherProfileResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<TeacherProfileResponse>.Fail("Đã xảy ra lỗi khi lấy thông tin", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin Teacher (FullName, PhoneNumber, AvatarUrl, SchoolName).
    /// </summary>
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateTeacherProfileRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();

            var hasAuthenticationFieldsToUpdate = request.FullName is not null
                || request.PhoneNumber is not null
                || request.AvatarUrl is not null;

            if (hasAuthenticationFieldsToUpdate)
            {
                await _authenticationService.UpdateCurrentUserAsync(userId, new UpdateCurrentUserRequest
                {
                    FullName = request.FullName,
                    PhoneNumber = request.PhoneNumber,
                    AvatarUrl = request.AvatarUrl
                });
            }

            await _teacherService.UpdateProfileAsync(userId, request);
            return Ok(ApiResponse<object>.Success(null, "Cập nhật thông tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for teacher {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi cập nhật thông tin", 500));
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
