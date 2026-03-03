using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Expert;
using EduVi.Services.Expert;
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
    private readonly ILogger<StaffController> _logger;

    public StaffController(IExpertService expertService, ILogger<StaffController> logger)
    {
        _expertService = expertService;
        _logger = logger;
    }

    /// <summary>
    /// Lấy danh sách hồ sơ Expert đang chờ duyệt (queue kiểm duyệt).
    /// Mỗi hồ sơ kèm Signed URL có hiệu lực 15 phút để Staff xem file trực tiếp.
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
    /// Xem chi tiết một hồ sơ (Signed URL mới được tạo mỗi lần gọi).
    /// </summary>
    [HttpGet("verifications/{verificationCode}")]
    public async Task<ActionResult<ApiResponse<ExpertVerificationStaffDto>>> GetVerificationDetail(string verificationCode)
    {
        try
        {
            var result = await _expertService.GetVerificationDetailAsync(verificationCode);
            return Ok(ApiResponse<ExpertVerificationStaffDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ExpertVerificationStaffDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching verification detail for {VerificationCode}", verificationCode);
            return StatusCode(500, ApiResponse<ExpertVerificationStaffDto>.Fail("Đã xảy ra lỗi", 500));
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
            var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
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
}
