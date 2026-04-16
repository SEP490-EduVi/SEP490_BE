using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Expert;
using EduVi.Contracts.DTOs.Profile;
using EduVi.Services.Expert;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Expert tự quản lý hồ sơ xác thực năng lực.
/// Tài khoản Expert mới đăng ký phải nộp hồ sơ tại đây trước khi được dùng các tính năng Expert.
/// </summary>
[ApiController]
[Route("api/expert")]
[Authorize(Policy = "AnyExpert")]
public class ExpertController : ControllerBase
{
    private readonly IExpertService _expertService;
    private readonly ILogger<ExpertController> _logger;

    public ExpertController(IExpertService expertService, ILogger<ExpertController> logger)
    {
        _expertService = expertService;
        _logger = logger;
    }

    /// <summary>
    /// Upload file chứng minh năng lực (bằng cấp, chứng chỉ, CCCD).
    /// Trạng thái ban đầu: pending — chờ Staff duyệt.
    /// </summary>
    [HttpPost("verifications")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ExpertVerificationDto>>> UploadVerification(
        [FromForm] UploadVerificationRequestDto request)
    {
        try
        {
            var expertId = GetCurrentUserId();
            var result = await _expertService.UploadVerificationAsync(expertId, request);
            return Ok(ApiResponse<ExpertVerificationDto>.Success(result, "Hồ sơ đã được nộp thành công. Vui lòng chờ nhân viên kiểm duyệt."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ExpertVerificationDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading verification for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<ExpertVerificationDto>.Fail("Đã xảy ra lỗi khi tải lên hồ sơ", 500));
        }
    }

    /// <summary>
    /// Xem danh sách hồ sơ đã nộp và trạng thái kiểm duyệt.
    /// Mỗi hồ sơ có FileUrl để xem lại file đã nộp.
    /// </summary>
    [HttpGet("verifications")]
    public async Task<ActionResult<ApiResponse<List<ExpertVerificationDto>>>> GetMyVerifications()
    {
        try
        {
            var expertId = GetCurrentUserId();
            var result = await _expertService.GetMyVerificationsAsync(expertId);
            return Ok(ApiResponse<List<ExpertVerificationDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting verifications for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<List<ExpertVerificationDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách hồ sơ", 500));
        }
    }

    /// <summary>
    /// Xem lại file chứng chỉ/bằng cấp đã nộp. Chỉ Expert sở hữu hồ sơ mới được truy cập.
    /// </summary>
    [HttpGet("verifications/{verificationCode}/file")]
    public async Task<IActionResult> GetMyVerificationFile(string verificationCode)
    {
        try
        {
            var expertId = GetCurrentUserId();
            var fileDto = await _expertService.GetMyVerificationFileAsync(expertId, verificationCode);
            return File(fileDto.FileBytes, fileDto.ContentType, fileDto.FileName);
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
            _logger.LogError(ex, "Error streaming verification file {VerificationCode} for expert {ExpertId}",
                verificationCode, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi tải file", 500));
        }
    }

    /// <summary>
    /// Xóa hồ sơ bị từ chối để nộp lại. Không thể xóa hồ sơ đã được duyệt.
    /// </summary>
    [HttpDelete("verifications/{verificationCode}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteVerification(string verificationCode)
    {
        try
        {
            var expertId = GetCurrentUserId();
            await _expertService.DeleteVerificationAsync(expertId, verificationCode);
            return Ok(ApiResponse<object>.Success(null, "Hồ sơ đã được xóa. Bạn có thể nộp lại."));
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
            _logger.LogError(ex, "Error deleting verification {VerificationCode}", verificationCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xóa hồ sơ", 500));
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
    /// Xem thông tin profile của Expert đang đăng nhập.
    /// </summary>
    [HttpGet("profile")]
    public async Task<ActionResult<ApiResponse<ExpertProfileResponse>>> GetProfile()
    {
        try
        {
            var expertId = GetCurrentUserId();
            var result = await _expertService.GetProfileAsync(expertId);
            return Ok(ApiResponse<ExpertProfileResponse>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ExpertProfileResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting profile for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<ExpertProfileResponse>.Fail("Đã xảy ra lỗi khi lấy thông tin", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin profile (FullName, PhoneNumber, Bio).
    /// </summary>
    [HttpPut("profile")]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfile([FromBody] UpdateExpertProfileRequest request)
    {
        try
        {
            var expertId = GetCurrentUserId();
            await _expertService.UpdateProfileAsync(expertId, request);
            return Ok(ApiResponse<object>.Success(null, "Cập nhật thông tin thành công."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating profile for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi cập nhật thông tin", 500));
        }
    }
}
