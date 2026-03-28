using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Expert;
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
            return StatusCode(500, ApiResponse<ExpertVerificationDto>.Fail("Đã xảy ra lỗi khi upload hồ sơ", 500));
        }
    }

    /// <summary>
    /// Xem danh sách hồ sơ đã nộp và trạng thái kiểm duyệt.
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
}
