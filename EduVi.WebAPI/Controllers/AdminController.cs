using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Admin.Request;
using EduVi.Contracts.DTOs.Admin.Response;
using EduVi.Contracts.DTOs.Material;
using EduVi.Services.Admin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Admin API — Quản lý người dùng, tài chính, gói cước.
/// Tất cả endpoint yêu cầu role "Admin".
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IAdminService adminService, ILogger<AdminController> logger)
    {
        _adminService = adminService;
        _logger = logger;
    }

    // ============================================================
    // 1. QUẢN LÝ NGƯỜI DÙNG (User Management)
    // ============================================================

    /// <summary>
    /// Danh sách người dùng với bộ lọc (role, trạng thái, tên, ngày đăng ký)
    /// </summary>
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminUserResponse>>>> GetUsers(
        [FromQuery] UserFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetUsersAsync(filter);
            return Ok(ApiResponse<PagedResponse<AdminUserResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting users");
            return StatusCode(500, ApiResponse<PagedResponse<AdminUserResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Tạo user mới (Admin/Staff/Expert/Teacher)
    /// </summary>
    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<AdminUserResponse>>> CreateUser([FromBody] CreateUserRequest request)
    {
        try
        {
            var result = await _adminService.CreateUserAsync(request);
            return CreatedAtAction(nameof(GetUserByCode), new { userCode = result.UserCode },
                ApiResponse<AdminUserResponse>.Success(result, "Tạo người dùng thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AdminUserResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating user with username {Username}", request.Username);
            return StatusCode(500, ApiResponse<AdminUserResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Xem chi tiết 1 user
    /// </summary>
    [HttpGet("users/{userCode}")]
    public async Task<ActionResult<ApiResponse<AdminUserResponse>>> GetUserByCode(string userCode)
    {
        try
        {
            var result = await _adminService.GetUserByCodeAsync(userCode);
            return Ok(ApiResponse<AdminUserResponse>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AdminUserResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting user {UserCode}", userCode);
            return StatusCode(500, ApiResponse<AdminUserResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Cập nhật thông tin cơ bản user (FullName, Phone, Avatar)
    /// </summary>
    [HttpPut("users/{userCode}")]
    public async Task<ActionResult<ApiResponse<AdminUserResponse>>> UpdateUser(
        string userCode, [FromBody] UpdateUserRequest request)
    {
        try
        {
            var result = await _adminService.UpdateUserAsync(userCode, request);
            return Ok(ApiResponse<AdminUserResponse>.Success(result, "Cập nhật thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<AdminUserResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating user {UserCode}", userCode);
            return StatusCode(500, ApiResponse<AdminUserResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Khóa tài khoản (Ban) + Revoke Token ngay lập tức
    /// </summary>
    [HttpPost("users/{userCode}/ban")]
    public async Task<ActionResult<ApiResponse<string>>> BanUser(string userCode)
    {
        try
        {
            await _adminService.BanUserAsync(userCode);
            return Ok(ApiResponse<string>.Success("", "Đã khóa tài khoản và thu hồi token"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning user {UserCode}", userCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Mở khóa tài khoản (Unban)
    /// </summary>
    [HttpPost("users/{userCode}/unban")]
    public async Task<ActionResult<ApiResponse<string>>> UnbanUser(string userCode)
    {
        try
        {
            await _adminService.UnbanUserAsync(userCode);
            return Ok(ApiResponse<string>.Success("", "Đã mở khóa tài khoản"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbanning user {UserCode}", userCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Xóa user (soft delete = ban) để tránh lỗi ràng buộc dữ liệu
    /// </summary>
    [HttpDelete("users/{userCode}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteUser(string userCode)
    {
        try
        {
            await _adminService.BanUserAsync(userCode);
            return Ok(ApiResponse<string>.Success("", "Đã khóa tài khoản (soft delete)"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error soft-deleting user {UserCode}", userCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Lấy danh sách tất cả roles (dùng cho dropdown phân quyền)
    /// </summary>
    [HttpGet("roles")]
    public async Task<ActionResult<ApiResponse<List<RoleResponse>>>> GetAllRoles()
    {
        try
        {
            var result = await _adminService.GetAllRolesAsync();
            return Ok(ApiResponse<List<RoleResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting roles");
            return StatusCode(500, ApiResponse<List<RoleResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    // ============================================================
    // 2. QUẢN LÝ TÀI CHÍNH & GIAO DỊCH (Financial & Transaction)
    // ============================================================

    /// <summary>
    /// Dashboard tài chính: tổng quan user, ví, giao dịch, doanh thu
    /// </summary>
    [HttpGet("financial/overview")]
    public async Task<ActionResult<ApiResponse<FinancialOverviewResponse>>> GetFinancialOverview()
    {
        try
        {
            var result = await _adminService.GetFinancialOverviewAsync();
            return Ok(ApiResponse<FinancialOverviewResponse>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial overview");
            return StatusCode(500, ApiResponse<FinancialOverviewResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Danh sách ví tất cả user (sắp xếp theo số dư giảm dần)
    /// </summary>
    [HttpGet("financial/wallets")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminWalletResponse>>>> GetAllWallets(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        try
        {
            var result = await _adminService.GetAllWalletsAsync(page, pageSize);
            return Ok(ApiResponse<PagedResponse<AdminWalletResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting wallets");
            return StatusCode(500, ApiResponse<PagedResponse<AdminWalletResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Lịch sử giao dịch toàn hệ thống (lọc theo user, loại, trạng thái, ngày)
    /// </summary>
    [HttpGet("financial/transactions")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminTransactionResponse>>>> GetAllTransactions(
        [FromQuery] TransactionFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetAllTransactionsAsync(filter);
            return Ok(ApiResponse<PagedResponse<AdminTransactionResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting transactions");
            return StatusCode(500, ApiResponse<PagedResponse<AdminTransactionResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Danh sách đơn hàng (lọc theo teacher, trạng thái, phương thức, ngày)
    /// </summary>
    [HttpGet("financial/orders")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminOrderResponse>>>> GetAllOrders(
        [FromQuery] OrderFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetAllOrdersAsync(filter);
            return Ok(ApiResponse<PagedResponse<AdminOrderResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting orders");
            return StatusCode(500, ApiResponse<PagedResponse<AdminOrderResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Dashboard doanh thu theo từng học liệu đã bán.
    /// </summary>
    [HttpGet("financial/revenue-by-material")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminMaterialSalesResponse>>>> GetRevenueByMaterial(
        [FromQuery] AdminMaterialSalesFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetMaterialSalesAnalyticsAsync(filter);
            return Ok(ApiResponse<PagedResponse<AdminMaterialSalesResponse>>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PagedResponse<AdminMaterialSalesResponse>>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue by material");
            return StatusCode(500, ApiResponse<PagedResponse<AdminMaterialSalesResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Dashboard doanh thu theo từng chuyên gia.
    /// </summary>
    [HttpGet("financial/revenue-by-expert")]
    public async Task<ActionResult<ApiResponse<PagedResponse<AdminExpertSalesResponse>>>> GetRevenueByExpert(
        [FromQuery] AdminExpertSalesFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetExpertSalesAnalyticsAsync(filter);
            return Ok(ApiResponse<PagedResponse<AdminExpertSalesResponse>>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PagedResponse<AdminExpertSalesResponse>>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting revenue by expert");
            return StatusCode(500, ApiResponse<PagedResponse<AdminExpertSalesResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Dashboard dự báo doanh thu dựa trên kỳ hiện tại và kỳ trước liền kề.
    /// </summary>
    [HttpGet("financial/forecast")]
    public async Task<ActionResult<ApiResponse<AdminRevenueForecastResponse>>> GetFinancialForecast(
        [FromQuery] AdminRevenueForecastFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetRevenueForecastAsync(filter);
            return Ok(ApiResponse<AdminRevenueForecastResponse>.Success(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<AdminRevenueForecastResponse>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting financial forecast");
            return StatusCode(500, ApiResponse<AdminRevenueForecastResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    // ============================================================
    // 3. QUẢN LÝ GÓI CƯỚC (Subscription Plans)
    // ============================================================

    /// <summary>
    /// Tất cả gói (bao gồm inactive) — dành cho Admin quản lý
    /// </summary>
    [HttpGet("plans")]
    public async Task<ActionResult<ApiResponse<List<PlanResponse>>>> GetAllPlans()
    {
        try
        {
            var result = await _adminService.GetAllPlansAsync();
            return Ok(ApiResponse<List<PlanResponse>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plans");
            return StatusCode(500, ApiResponse<List<PlanResponse>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Chi tiết 1 gói
    /// </summary>
    [HttpGet("plans/{planId}")]
    public async Task<ActionResult<ApiResponse<PlanResponse>>> GetPlanById(int planId)
    {
        try
        {
            var result = await _adminService.GetPlanByIdAsync(planId);
            return Ok(ApiResponse<PlanResponse>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PlanResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting plan {PlanId}", planId);
            return StatusCode(500, ApiResponse<PlanResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Tạo gói cước mới
    /// </summary>
    [HttpPost("plans")]
    public async Task<ActionResult<ApiResponse<PlanResponse>>> CreatePlan([FromBody] CreatePlanRequest request)
    {
        try
        {
            var result = await _adminService.CreatePlanAsync(request);
            return CreatedAtAction(nameof(GetPlanById), new { planId = result.PlanId },
                ApiResponse<PlanResponse>.Success(result, "Tạo gói cước thành công"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating plan");
            return StatusCode(500, ApiResponse<PlanResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Cập nhật gói cước (partial update — chỉ gửi field cần sửa)
    /// </summary>
    [HttpPut("plans/{planId}")]
    public async Task<ActionResult<ApiResponse<PlanResponse>>> UpdatePlan(
        int planId, [FromBody] UpdatePlanRequest request)
    {
        try
        {
            var result = await _adminService.UpdatePlanAsync(planId, request);
            return Ok(ApiResponse<PlanResponse>.Success(result, "Cập nhật gói cước thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PlanResponse>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating plan {PlanId}", planId);
            return StatusCode(500, ApiResponse<PlanResponse>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Xóa gói (soft delete — IsActive = false)
    /// </summary>
    [HttpDelete("plans/{planId}")]
    public async Task<ActionResult<ApiResponse<string>>> DeletePlan(int planId)
    {
        try
        {
            await _adminService.DeletePlanAsync(planId);
            return Ok(ApiResponse<string>.Success("", "Đã ẩn gói cước"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting plan {PlanId}", planId);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi hệ thống", 500));
        }
    }

    // ============================================================
    // 4. QUẢN LÝ HỌC LIỆU (Material CRUD)
    // ============================================================

    /// <summary>
    /// Danh sách học liệu với bộ lọc, phân trang cho Admin.
    /// </summary>
    [HttpGet("materials")]
    public async Task<ActionResult<ApiResponse<PagedResponse<MaterialResponseDto>>>> GetMaterials(
        [FromQuery] AdminMaterialFilterRequest filter)
    {
        try
        {
            var result = await _adminService.GetMaterialsForAdminAsync(filter);
            return Ok(ApiResponse<PagedResponse<MaterialResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting materials for admin dashboard");
            return StatusCode(500, ApiResponse<PagedResponse<MaterialResponseDto>>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Chi tiết học liệu theo MaterialCode.
    /// </summary>
    [HttpGet("materials/{materialCode}")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> GetMaterialByCode(string materialCode)
    {
        try
        {
            var result = await _adminService.GetMaterialDetailForAdminAsync(materialCode);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting material detail for code {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Tạo học liệu mới bởi Admin.
    /// </summary>
    [HttpPost("materials")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> CreateMaterial([FromBody] CreateAdminMaterialRequest request)
    {
        try
        {
            var result = await _adminService.CreateMaterialForAdminAsync(request);
            return CreatedAtAction(nameof(GetMaterialByCode), new { materialCode = result.MaterialCode },
                ApiResponse<MaterialResponseDto>.Success(result, "Tạo học liệu thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating material for admin");
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Cập nhật học liệu theo MaterialCode bởi Admin.
    /// </summary>
    [HttpPut("materials/{materialCode}")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> UpdateMaterial(
        string materialCode,
        [FromBody] UpdateAdminMaterialRequest request)
    {
        try
        {
            var result = await _adminService.UpdateMaterialForAdminAsync(materialCode, request);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result, "Cập nhật học liệu thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating material {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Lỗi hệ thống", 500));
        }
    }

    /// <summary>
    /// Soft delete học liệu theo MaterialCode bởi Admin (ẩn khỏi marketplace).
    /// </summary>
    [HttpDelete("materials/{materialCode}")]
    public async Task<ActionResult<ApiResponse<string>>> DeleteMaterial(string materialCode)
    {
        try
        {
            await _adminService.DeleteMaterialForAdminAsync(materialCode);
            return Ok(ApiResponse<string>.Success(string.Empty, "Đã ẩn học liệu khỏi marketplace"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting material {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi hệ thống", 500));
        }
    }
}
