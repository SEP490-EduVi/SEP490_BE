using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Authentication.Request;
using EduVi.Contracts.DTOs.Expert;
using EduVi.Contracts.DTOs.Profile;
using EduVi.Services.Authentication;
using EduVi.Services.Expert;
using EduVi.Services.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Staff kiểm duyệt hồ sơ năng lực của Expert.
/// </summary>
[ApiController]
[Route("api/staff")]
[Authorize(Roles = "Staff")]
public class StaffController : ControllerBase
{
    private readonly IExpertService _expertService;
    private readonly IStaffProfileService _staffProfileService;
    private readonly IAuthenticationService _authenticationService;
    private readonly ILogger<StaffController> _logger;

    public StaffController(
        IExpertService expertService,
        IStaffProfileService staffProfileService,
        IAuthenticationService authenticationService,
        ILogger<StaffController> logger)
    {
        _expertService = expertService;
        _staffProfileService = staffProfileService;
        _authenticationService = authenticationService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách hồ sơ Expert đang chờ duyệt (queue kiểm duyệt).
    /// </summary>
    [HttpGet("verifications/pending")]
    public async Task<ActionResult<ApiResponse<List<ExpertVerificationStaffDto>>>> GetPendingVerifications()
    {
        try
        {
            var result = await _expertService.GetPendingVerificationsAsync();
            return Ok(ApiResponse<List<ExpertVerificationStaffDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending expert verifications");
            return StatusCode(500, ApiResponse<List<ExpertVerificationStaffDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách hồ sơ", 500));
        }
    }

    /// <summary>
    /// Proxy file verification qua backend để Staff xem trực tiếp mà không cần Signed URL.
    /// </summary>
    [HttpGet("verifications/{verificationCode}/file")]
    public async Task<IActionResult> GetVerificationFile(string verificationCode)
    {
        try
        {
            var filePayload = await _expertService.GetVerificationFileAsync(verificationCode);
            return File(filePayload.FileBytes, filePayload.ContentType, filePayload.FileName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proxying verification file for {VerificationCode}", verificationCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi tải file hồ sơ", 500));
        }
    }

    /// <summary>
    /// Phê duyệt hoặc từ chối hồ sơ Expert.
    /// Approve → Expert.IsVerified = true, token cũ bị invalidate (Expert phải đăng nhập lại).
    /// Reject  → hồ sơ bị đánh dấu rejected kèm lý do, Expert cần nộp lại.
    /// </summary>
    [HttpPost("verifications/{verificationCode}/review")]
    public async Task<ActionResult<ApiResponse<object>>> ReviewVerification(
        string verificationCode,
        [FromBody] ReviewVerificationRequestDto request)
    {
        try
        {
            var staffId = GetCurrentUserId();
            await _expertService.ReviewVerificationAsync(staffId, verificationCode, request);

            var message = request.Approved
                ? "Hồ sơ đã được phê duyệt. Expert sẽ nhận tích xanh sau khi đăng nhập lại."
                : "Hồ sơ đã bị từ chối. Expert sẽ nhận thông báo yêu cầu bổ sung.";

            return Ok(ApiResponse<object>.Success(null, message));
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
            _logger.LogError(ex, "Error reviewing verification {VerificationCode} by staff {StaffId}",
                verificationCode, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xử lý hồ sơ", 500));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy ID người dùng trong token");
        return userId;
    }

    /// <summary>
    /// Xem thông tin profile của Staff đang đăng nhập.
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<StaffProfileResponse>>> GetProfile()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _staffProfileService.GetProfileAsync(userId);
            return Ok(ApiResponse<StaffProfileResponse>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<StaffProfileResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for staff {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<StaffProfileResponse>.Fail("Đã xảy ra lỗi khi lấy thông tin", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin Staff (FullName, PhoneNumber, AvatarUrl, Department).
    /// </summary>
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateStaffProfileRequest request)
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

            await _staffProfileService.UpdateProfileAsync(userId, request);
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
            _logger.LogError(ex, "Error updating profile for staff {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi cập nhật thông tin", 500));
        }
    }
}
