using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Services.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProductController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly ILogger<ProductController> _logger;

    public ProductController(IPipelineService pipelineService, ILogger<ProductController> logger)
    {
        _pipelineService = pipelineService;
        _logger = logger;
    }

    // =====================================================================
    // LIST
    // =====================================================================

    /// <summary>
    /// Lấy danh sách tất cả Products của Teacher hiện tại (không bao gồm đã xóa)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductSummaryDto>>>> GetMyProducts()
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _pipelineService.GetProductsByTeacherAsync(teacherId);
            return Ok(ApiResponse<List<ProductSummaryDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products for teacher");
            return StatusCode(500, ApiResponse<List<ProductSummaryDto>>.Fail("Lỗi khi lấy danh sách product", 500));
        }
    }

    // =====================================================================
    // DETAIL
    // =====================================================================

    /// <summary>
    /// Lấy chi tiết đầy đủ của một Product — bao gồm dữ liệu của tất cả các bước pipeline
    /// </summary>
    [HttpGet("{productCode}")]
    public async Task<ActionResult<ApiResponse<ProductDetailDto>>> GetProduct(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _pipelineService.GetProductByCodeAsync(teacherId, productCode);
            return Ok(ApiResponse<ProductDetailDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProductDetailDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<ProductDetailDto>.Fail("Lỗi khi lấy thông tin product", 500));
        }
    }

    // =====================================================================
    // STAGE-SPECIFIC READS
    // =====================================================================

    /// <summary>
    /// Lấy kết quả đánh giá AI (EvaluationResult) của Product.
    /// Trả về 409 nếu bước evaluation chưa hoàn thành.
    /// </summary>
    [HttpGet("{productCode}/evaluation")]
    public async Task<ActionResult<ApiResponse<object>>> GetEvaluation(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var product = await _pipelineService.GetProductByCodeAsync(teacherId, productCode);

            if (!product.EvaluationResult.HasValue)
                return Conflict(ApiResponse<object>.Fail(
                    "Kết quả đánh giá chưa có. Vui lòng chờ quá trình phân tích bài giảng hoàn tất", 409));

            return Ok(ApiResponse<object>.Success(new
            {
                product.EvaluationResult,
                product.EvaluatedAt,
            }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching evaluation for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy kết quả đánh giá", 500));
        }
    }

    /// <summary>
    /// Lấy slide do AI tạo ra (SlideDocument — bản gốc, không bị ghi đè bởi Teacher edit).
    /// Trả về 409 nếu slide chưa được generate.
    /// </summary>
    [HttpGet("{productCode}/slide")]
    public async Task<ActionResult<ApiResponse<object>>> GetAiSlide(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var product = await _pipelineService.GetProductByCodeAsync(teacherId, productCode);

            if (!product.SlideDocument.HasValue)
                return Conflict(ApiResponse<object>.Fail(
                    "Slide chưa được tạo. Vui lòng chờ quá trình tạo slide hoàn tất", 409));

            return Ok(ApiResponse<object>.Success(new
            {
                product.SlideDocument,
                product.SlideGeneratedAt
            }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching AI slide for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy slide AI", 500));
        }
    }

    /// <summary>
    /// Lấy slide đã được Teacher chỉnh sửa lần cuối (SlideEditedDocument).
    /// Trả về 409 nếu Teacher chưa lưu bản chỉnh sửa nào.
    /// </summary>
    [HttpGet("{productCode}/slide/edited")]
    public async Task<ActionResult<ApiResponse<object>>> GetEditedSlide(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var product = await _pipelineService.GetProductByCodeAsync(teacherId, productCode);

            if (!product.SlideEditedDocument.HasValue)
                return Conflict(ApiResponse<object>.Fail(
                    "Chưa có bản chỉnh sửa nào được lưu. Vui lòng chỉnh sửa slide trước", 409));

            return Ok(ApiResponse<object>.Success(new
            {
                product.SlideEditedDocument,
                product.SlideEditedAt
            }));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching edited slide for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lấy slide đã chỉnh sửa", 500));
        }
    }

    // =====================================================================
    // SOFT DELETE
    // =====================================================================

    /// <summary>
    /// Xóa mềm Product (đánh dấu trạng thái Deleted).
    /// Không thể xóa khi Product đang được AI xử lý.
    /// </summary>
    [HttpDelete("{productCode}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            await _pipelineService.DeleteProductAsync(teacherId, productCode);
            return Ok(ApiResponse<object>.Success(null, "Product đã được xóa thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ApiResponse<object>.Fail(ex.Message, 409));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa product", 500));
        }
    }

    // =====================================================================
    // HELPER
    // =====================================================================

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User ID not found in token");
        return userId;
    }
}
