using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Payment.Request;
using EduVi.Contracts.DTOs.Payment.Response;
using EduVi.Services.Payment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(IPaymentService paymentService, ILogger<PaymentController> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    // =====================================================================
    // GÓI SUBSCRIPTION
    // =====================================================================

    /// <summary>
    /// Lấy danh sách gói subscription (public - không cần đăng nhập)
    /// </summary>
    [HttpGet("plans")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SubscriptionPlanResponse>>>> GetPlans()
    {
        try
        {
            var plans = await _paymentService.GetAllPlansAsync();
            return Ok(ApiResponse<List<SubscriptionPlanResponse>>.Success(plans));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subscription plans");
            return StatusCode(500, ApiResponse<List<SubscriptionPlanResponse>>.Fail("Lỗi khi lấy danh sách gói", 500));
        }
    }

    // =====================================================================
    // VÍ EDUCOIN
    // =====================================================================

    /// <summary>
    /// Lấy thông tin ví EduCoin của user hiện tại
    /// </summary>
    [HttpGet("wallet")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<WalletResponse>>> GetWallet()
    {
        try
        {
            var userId = GetCurrentUserId();
            var wallet = await _paymentService.GetWalletAsync(userId);
            return Ok(ApiResponse<WalletResponse>.Success(wallet));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallet");
            return StatusCode(500, ApiResponse<WalletResponse>.Fail("Lỗi khi lấy thông tin ví", 500));
        }
    }

    /// <summary>
    /// Lấy thông tin quota hiện tại của giáo viên đăng nhập.
    /// </summary>
    [HttpGet("user-quota")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserQuotaResponse>>> GetUserQuota()
    {
        try
        {
            var userId = GetCurrentUserId();
            var quota = await _paymentService.GetUserQuotaAsync(userId);
            return Ok(ApiResponse<UserQuotaResponse>.Success(quota));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<UserQuotaResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user quota");
            return StatusCode(500, ApiResponse<UserQuotaResponse>.Fail("Lỗi khi lấy thông tin quota", 500));
        }
    }

    // =====================================================================
    // NẠP TIỀN QUA PAYOS
    // =====================================================================

    /// <summary>
    /// Tạo link thanh toán PayOS để nạp EduCoin.
    /// FE redirect user đến checkoutUrl trả về.
    /// </summary>
    [HttpPost("top-up")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TopUpResponse>>> CreateTopUp([FromBody] TopUpRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _paymentService.CreateTopUpAsync(userId, request);
            return Ok(ApiResponse<TopUpResponse>.Success(result, "Đã tạo link thanh toán"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TopUpResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating top-up");
            return StatusCode(500, ApiResponse<TopUpResponse>.Fail("Lỗi khi tạo giao dịch nạp tiền", 500));
        }
    }

    /// <summary>
    /// Webhook endpoint cho PayOS gọi khi thanh toán hoàn tất.
    /// KHÔNG CẦN AUTH - PayOS gọi trực tiếp, verify bằng signature.
    /// </summary>
    [HttpPost("payos-webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> PayOSWebhook([FromBody] WebhookType webhookBody)
    {
        try
        {
            var result = await _paymentService.HandlePayOSWebhookAsync(webhookBody);

            if (result)
                return Ok(new { success = true });
            else
                return BadRequest(new { success = false, message = "Invalid webhook" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PayOS webhook");
            // PHẢI trả OK để PayOS không gửi lại (retry storm)
            // Log lỗi để xử lý thủ công
            return Ok(new { success = false, message = "Lỗi hệ thống (liên quan tới payos-webhook" });
        }
    }

    /// <summary>
    /// Kiểm tra trạng thái giao dịch nạp tiền (FE gọi sau khi redirect về)
    /// </summary>
    [HttpGet("top-up/verify/{orderCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<TransactionHistoryResponse>>> VerifyTopUp(long orderCode)
    {
        try
        {
            var result = await _paymentService.VerifyTopUpByOrderCodeAsync(orderCode);
            if (result == null)
                return NotFound(ApiResponse<TransactionHistoryResponse>.Fail("Không tìm thấy giao dịch", 404));

            return Ok(ApiResponse<TransactionHistoryResponse>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verifying top-up. OrderCode={OrderCode}", orderCode);
            return StatusCode(500, ApiResponse<TransactionHistoryResponse>.Fail("Lỗi khi kiểm tra giao dịch", 500));
        }
    }

    // =====================================================================
    // MUA GÓI BẰNG EDUCOIN
    // =====================================================================

    /// <summary>
    /// Mua gói subscription bằng EduCoin trong ví.
    /// Trừ tiền → cộng quota cho Teacher.
    /// </summary>
    [HttpPost("buy-subscription")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<BuySubscriptionResponse>>> BuySubscription(
        [FromBody] BuySubscriptionRequest request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _paymentService.BuySubscriptionAsync(userId, request);
            return Ok(ApiResponse<BuySubscriptionResponse>.Success(result, "Mua gói thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<BuySubscriptionResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error buying subscription");
            return StatusCode(500, ApiResponse<BuySubscriptionResponse>.Fail("Lỗi khi mua gói", 500));
        }
    }

    // =====================================================================
    // LỊCH SỬ GIAO DỊCH
    // =====================================================================

    /// <summary>
    /// Lấy lịch sử giao dịch ví (có phân trang)
    /// </summary>
    [HttpGet("transactions")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = GetCurrentUserId();
            var (items, totalCount) = await _paymentService.GetTransactionHistoryAsync(userId, page, pageSize);

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
            _logger.LogError(ex, "Error getting transaction history");
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy lịch sử giao dịch", 500));
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
