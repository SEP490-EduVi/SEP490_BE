using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.ProductMaterial;
using EduVi.Services.ProductMaterials;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/products/{productCode}/materials")]
[Authorize(Roles = "Teacher")]
public class ProductMaterialController : ControllerBase
{
    private readonly IProductMaterialService _productMaterialService;
    private readonly ILogger<ProductMaterialController> _logger;

    public ProductMaterialController(IProductMaterialService productMaterialService, ILogger<ProductMaterialController> logger)
    {
        _productMaterialService = productMaterialService;
        _logger = logger;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<ProductMaterialResponseDto>>> CreateProductMaterial(
        string productCode,
        [FromBody] CreateProductMaterialRequestDto request)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _productMaterialService.CreateProductMaterialAsync(teacherId, productCode, request);
            return Ok(ApiResponse<ProductMaterialResponseDto>.Success(result, "Học liệu đã được thêm vào sản phẩm"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<ProductMaterialResponseDto>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<ProductMaterialResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error creating product material for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<ProductMaterialResponseDto>.Fail("Đã xảy ra lỗi khi thêm học liệu vào sản phẩm", 500));
        }
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductMaterialResponseDto>>>> GetProductMaterials(string productCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _productMaterialService.GetProductMaterialsAsync(teacherId, productCode);
            return Ok(ApiResponse<List<ProductMaterialResponseDto>>.Success(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<List<ProductMaterialResponseDto>>.Fail(exception.Message, 404));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error getting product materials for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<List<ProductMaterialResponseDto>>.Fail("Đã xảy ra lỗi khi lấy danh sách học liệu sản phẩm", 500));
        }
    }

    [HttpGet("{productMaterialCode}")]
    public async Task<ActionResult<ApiResponse<ProductMaterialResponseDto>>> GetProductMaterial(string productCode, string productMaterialCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _productMaterialService.GetProductMaterialAsync(teacherId, productCode, productMaterialCode);
            return Ok(ApiResponse<ProductMaterialResponseDto>.Success(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<ProductMaterialResponseDto>.Fail(exception.Message, 404));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error getting product material {ProductMaterialCode} for product {ProductCode}", productMaterialCode, productCode);
            return StatusCode(500, ApiResponse<ProductMaterialResponseDto>.Fail("Đã xảy ra lỗi khi lấy chi tiết học liệu sản phẩm", 500));
        }
    }

    [HttpPut("{productMaterialCode}")]
    public async Task<ActionResult<ApiResponse<ProductMaterialResponseDto>>> UpdateProductMaterial(
        string productCode,
        string productMaterialCode,
        [FromBody] UpdateProductMaterialRequestDto request)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _productMaterialService.UpdateProductMaterialAsync(teacherId, productCode, productMaterialCode, request);
            return Ok(ApiResponse<ProductMaterialResponseDto>.Success(result, "Học liệu sản phẩm đã được cập nhật"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<ProductMaterialResponseDto>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<ProductMaterialResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error updating product material {ProductMaterialCode} for product {ProductCode}", productMaterialCode, productCode);
            return StatusCode(500, ApiResponse<ProductMaterialResponseDto>.Fail("Đã xảy ra lỗi khi cập nhật học liệu sản phẩm", 500));
        }
    }

    [HttpDelete("{productMaterialCode}")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteProductMaterial(string productCode, string productMaterialCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            await _productMaterialService.DeleteProductMaterialAsync(teacherId, productCode, productMaterialCode);
            return Ok(ApiResponse<object>.Success(null, "Học liệu đã được xóa khỏi sản phẩm"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail(exception.Message, 404));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error deleting product material {ProductMaterialCode} for product {ProductCode}", productMaterialCode, productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Đã xảy ra lỗi khi xóa học liệu sản phẩm", 500));
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
