using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Material;
using EduVi.Services.Material;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Material marketplace — Expert upload, Staff duyệt, Teacher browse/mua.
/// </summary>
[ApiController]
[Route("api/material")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _materialService;
    private readonly ILogger<MaterialController> _logger;

    public MaterialController(IMaterialService materialService, ILogger<MaterialController> logger)
    {
        _materialService = materialService;
        _logger = logger;
    }

    // =====================================================================
    // EXPERT: quản lý materials
    // =====================================================================

    /// <summary>
    /// [Expert] Upload học liệu dạng file (image, video).
    /// Trạng thái ban đầu: pending — chờ Staff duyệt.
    /// </summary>
    [HttpPost("file")]
    [Authorize(Policy = "VerifiedExpert")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> UploadFileMaterial(
        [FromForm] UploadFileMaterialRequestDto request)
    {
        try
        {
            var expertId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.UploadFileMaterialAsync(expertId, request);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result, "File học liệu đã được upload thành công. Vui lòng chờ nhân viên kiểm duyệt."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 400));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file material for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Đã xảy ra lỗi khi upload học liệu", 500));
        }
    }

    /// <summary>
    /// [Expert] Xem danh sách materials đã upload.
    /// </summary>
    [HttpGet("my")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<List<MaterialResponseDto>>>> GetMyMaterials()
    {
        try
        {
            var expertId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.GetMyMaterialsAsync(expertId);
            return Ok(ApiResponse<List<MaterialResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting materials for expert {ExpertId}", User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<List<MaterialResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách học liệu", 500));
        }
    }

    /// <summary>
    /// [Expert] Cập nhật material (chỉ được khi chưa approve).
    /// Sau khi sửa, ApprovalStatus reset về pending → cần duyệt lại.
    /// </summary>
    [HttpPut("{materialCode}")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> UpdateMaterial(
        string materialCode, [FromBody] UpdateMaterialRequestDto request)
    {
        try
        {
            var expertId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.UpdateMaterialAsync(expertId, materialCode, request);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result, "Học liệu đã được cập nhật. Vui lòng chờ nhân viên duyệt lại."));
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
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Đã xảy ra lỗi khi cập nhật học liệu", 500));
        }
    }

    /// <summary>
    /// [Expert] Xóa material (chỉ được khi chưa approve).
    /// </summary>
    [HttpDelete("{materialCode}")]
    [Authorize(Policy = "VerifiedExpert")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteMaterial(string materialCode)
    {
        try
        {
            var expertId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _materialService.DeleteMaterialAsync(expertId, materialCode);
            return Ok(ApiResponse<object>.Success(null, "Material đã được xóa."));
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
            _logger.LogError(ex, "Error deleting material {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xóa học liệu", 500));
        }
    }

    // =====================================================================
    // STAFF: kiểm duyệt materials
    // =====================================================================

    /// <summary>
    /// [Staff] Lấy danh sách materials đang chờ duyệt.
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Staff")]
    public async Task<ActionResult<ApiResponse<List<MaterialResponseDto>>>> GetPendingMaterials()
    {
        try
        {
            var result = await _materialService.GetPendingMaterialsAsync();
            return Ok(ApiResponse<List<MaterialResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching pending materials");
            return StatusCode(500, ApiResponse<List<MaterialResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách học liệu chờ duyệt", 500));
        }
    }

    /// <summary>
    /// [Staff] Xem chi tiết 1 material (kèm Resource URL để review nội dung).
    /// </summary>
    [HttpGet("review/{materialCode}")]
    [Authorize(Roles = "Staff")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> GetMaterialForReview(string materialCode)
    {
        try
        {
            var result = await _materialService.GetMaterialDetailForStaffAsync(materialCode);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching material detail {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Đã xảy ra lỗi", 500));
        }
    }

    /// <summary>
    /// [Staff] Phê duyệt hoặc từ chối material.
    /// </summary>
    [HttpPost("{materialCode}/review")]
    [Authorize(Roles = "Staff")]
    public async Task<ActionResult<ApiResponse<object>>> ReviewMaterial(
        string materialCode, [FromBody] ReviewMaterialRequestDto request)
    {
        try
        {
            var staffId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _materialService.ReviewMaterialAsync(staffId, materialCode, request);

            var message = request.Approved
                ? "Material đã được phê duyệt. Teacher có thể mua từ marketplace."
                : "Material đã bị từ chối. Expert sẽ nhận thông báo.";

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
            _logger.LogError(ex, "Error reviewing material {MaterialCode} by staff {StaffId}",
                materialCode, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi duyệt học liệu", 500));
        }
    }

    // =====================================================================
    // TEACHER: browse và mua materials
    // =====================================================================

    /// <summary>
    /// [Teacher] Browse danh sách materials đã duyệt (có thể lọc theo subject, grade, type, keyword).
    /// ResourceUrl không trả về cho materials chưa mua.
    /// </summary>
    [HttpGet("browse")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<List<MaterialResponseDto>>>> BrowseMaterials(
        [FromQuery] string? subjectCode,
        [FromQuery] string? gradeCode,
        [FromQuery] string? type,
        [FromQuery] string? keyword)
    {
        try
        {
            var result = await _materialService.BrowseMaterialsAsync(subjectCode, gradeCode, type, keyword);
            return Ok(ApiResponse<List<MaterialResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error browsing materials");
            return StatusCode(500, ApiResponse<List<MaterialResponseDto>>.Fail("Đã xảy ra lỗi khi duyệt danh sách học liệu", 500));
        }
    }

    /// <summary>
    /// [Teacher] Xem chi tiết 1 material. Nếu đã mua → trả về ResourceUrl.
    /// </summary>
    [HttpGet("{materialCode}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<MaterialResponseDto>>> GetMaterialDetail(string materialCode)
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.GetMaterialDetailForTeacherAsync(teacherId, materialCode);
            return Ok(ApiResponse<MaterialResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<MaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting material detail {MaterialCode}", materialCode);
            return StatusCode(500, ApiResponse<MaterialResponseDto>.Fail("Đã xảy ra lỗi", 500));
        }
    }

    /// <summary>
    /// [Teacher] Mua material — trừ tiền ví, tạo TeacherMaterials record.
    /// Material miễn phí (price = 0) vẫn tạo record để tracking ownership.
    /// </summary>
    [HttpPost("{materialCode}/purchase")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<PurchasedMaterialResponseDto>>> PurchaseMaterial(string materialCode)
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.PurchaseMaterialAsync(teacherId, materialCode);
            return Ok(ApiResponse<PurchasedMaterialResponseDto>.Success(result, "Mua học liệu thành công!"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<PurchasedMaterialResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PurchasedMaterialResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error purchasing material {MaterialCode} by teacher {TeacherId}",
                materialCode, User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<PurchasedMaterialResponseDto>.Fail("Đã xảy ra lỗi khi mua học liệu", 500));
        }
    }

    /// <summary>
    /// [Teacher] Xem danh sách materials đã mua.
    /// </summary>
    [HttpGet("purchased")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<List<PurchasedMaterialResponseDto>>>> GetPurchasedMaterials()
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _materialService.GetPurchasedMaterialsAsync(teacherId);
            return Ok(ApiResponse<List<PurchasedMaterialResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting purchased materials for teacher {TeacherId}",
                User.FindFirstValue(ClaimTypes.NameIdentifier));
            return StatusCode(500, ApiResponse<List<PurchasedMaterialResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách đã mua", 500));
        }
    }
}
