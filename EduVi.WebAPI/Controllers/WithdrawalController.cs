using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Withdrawal.Request;
using EduVi.Contracts.DTOs.Withdrawal.Response;
using EduVi.Services.Withdrawal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/withdrawal")]
public class WithdrawalController : ControllerBase
{
    private readonly IWithdrawalService _withdrawalService;
    private readonly ILogger<WithdrawalController> _logger;

    public WithdrawalController(IWithdrawalService withdrawalService, ILogger<WithdrawalController> logger)
    {
        _withdrawalService = withdrawalService;
        _logger = logger;
    }

    // =====================================================================
    // USER (Expert): Yêu cầu rút tiền
    // =====================================================================

    /// <summary>
    /// [Expert] Bước 1: Nhập thông tin ngân hàng + số tiền → nhận OTP qua email.
    /// OTP hết hạn sau 5 phút. Giới hạn 5 lần / 5 phút.
    /// </summary>
    [HttpPost("initiate")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<object>>> InitiateWithdrawal(
        [FromBody] InitiateWithdrawalRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _withdrawalService.SendWithdrawalOtpAsync(userId, request);
            return Ok(ApiResponse<object>.Success(null, "Mã OTP đã được gửi đến email của bạn. Vui lòng xác nhận trong 5 phút."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating withdrawal for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi gửi OTP xác nhận rút tiền", 500));
        }
    }

    /// <summary>
    /// [Expert] Bước 2: Xác nhận OTP → tạo yêu cầu rút tiền, tiền bị freeze trong ví.
    /// </summary>
    [HttpPost("confirm")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> ConfirmWithdrawal(
        [FromBody] ConfirmWithdrawalOtpRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _withdrawalService.ConfirmWithdrawalAsync(userId, request);
            return Ok(ApiResponse<WithdrawalResponse>.Success(result, "Yêu cầu rút tiền đã được ghi nhận. Admin sẽ xử lý và chuyển khoản trong thời gian sớm nhất."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<WithdrawalResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming withdrawal for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<WithdrawalResponse>.Fail("Lỗi khi tạo yêu cầu rút tiền", 500));
        }
    }

    /// <summary>
    /// [Expert] Xem lịch sử yêu cầu rút tiền của chính mình.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<object>>> GetMyWithdrawals(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var (items, totalCount) = await _withdrawalService.GetMyWithdrawalsAsync(userId, page, pageSize);

            var response = new
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<object>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting withdrawal history for user {UserId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy lịch sử rút tiền", 500));
        }
    }

    // =====================================================================
    // ADMIN: Quản lý yêu cầu rút tiền
    // =====================================================================

    /// <summary>
    /// [Admin] Xem tất cả yêu cầu rút tiền. Có thể lọc theo status: CONFIRMED, SUCCESS, REJECTED.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> GetAllWithdrawals(
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var (items, totalCount) = await _withdrawalService.GetAllWithdrawalsAsync(status, page, pageSize);

            var response = new
            {
                Items = items,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };

            return Ok(ApiResponse<object>.Success(response));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all withdrawals");
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy danh sách yêu cầu rút tiền", 500));
        }
    }

    /// <summary>
    /// [Admin] Duyệt hoặc từ chối yêu cầu rút tiền.
    /// Approved=true → SUCCESS (ghi transaction, giữ nguyên balance đã freeze).
    /// Approved=false → REJECTED (hoàn lại tiền vào ví).
    /// </summary>
    [HttpPost("{withdrawalId}/process")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<WithdrawalResponse>>> ProcessWithdrawal(
        int withdrawalId, [FromBody] AdminProcessWithdrawalRequest request)
    {
        try
        {
            var adminUserId = GetCurrentUserId();
            var result = await _withdrawalService.ProcessWithdrawalAsync(adminUserId, withdrawalId, request);

            var message = request.Approved
                ? "Yêu cầu rút tiền đã được duyệt thành công."
                : "Yêu cầu rút tiền đã bị từ chối. Tiền đã được hoàn lại vào ví.";

            return Ok(ApiResponse<WithdrawalResponse>.Success(result, message));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<WithdrawalResponse>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<WithdrawalResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing withdrawal {WithdrawalId}", withdrawalId);
            return StatusCode(500, ApiResponse<WithdrawalResponse>.Fail("Lỗi khi xử lý yêu cầu rút tiền", 500));
        }
    }

    // =====================================================================
    // HELPER
    // =====================================================================

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");
        return userId;
    }
}
