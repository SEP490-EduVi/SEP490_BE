using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Services.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Teacher")]
public class VideoController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly ILogger<VideoController> _logger;

    public VideoController(
        IPipelineService pipelineService,
        ILogger<VideoController> logger)
    {
        _pipelineService = pipelineService;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<ProductVideoDetailDto>>>> GetVideos()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.GetProductVideosByTeacherAsync(userId);
            return Ok(ApiResponse<List<ProductVideoDetailDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách video của giáo viên hiện tại");
            return StatusCode(500, ApiResponse<List<ProductVideoDetailDto>>.Fail("Lỗi khi lấy danh sách video", 500));
        }
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<PipelineTaskResponseDto>>> GenerateVideo(
        [FromBody] GenerateVideoRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.CreateVideoGenerationTaskAsync(userId, request);
            return Ok(ApiResponse<PipelineTaskResponseDto>.Success(result, "Task tạo video đã được đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PipelineTaskResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi tạo task tạo video");
            return StatusCode(500, ApiResponse<PipelineTaskResponseDto>.Fail("Lỗi khi tạo task tạo video", 500));
        }
    }

    [HttpGet("project/{projectCode}")]
    public async Task<ActionResult<ApiResponse<List<ProductVideoDetailDto>>>> GetVideosByProjectCode(string projectCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.GetProductVideosByProjectCodeAsync(userId, projectCode);
            return Ok(ApiResponse<List<ProductVideoDetailDto>>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<List<ProductVideoDetailDto>>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lấy danh sách video cho dự án {ProjectCode}", projectCode);
            return StatusCode(500, ApiResponse<List<ProductVideoDetailDto>>.Fail("Lỗi khi lấy danh sách video", 500));
        }
    }

    [HttpGet("document/{documentCode}/latest")]
    public async Task<ActionResult<ApiResponse<ProductVideoDetailDto>>> GetVideoByDocumentCode(string documentCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.GetLatestProductVideoByDocumentCodeAsync(userId, documentCode);
            return Ok(ApiResponse<ProductVideoDetailDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProductVideoDetailDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting latest product video for document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<ProductVideoDetailDto>.Fail("Lỗi khi lấy thông tin video", 500));
        }
    }

    //[HttpGet("product/{productCode}/latest")]
    //[Authorize]
    //public async Task<ActionResult<ApiResponse<ProductVideoDetailDto>>> GetLatestVideoByProductCode(string productCode)
    //{
    //    try
    //    {
    //        var userId = GetCurrentUserId();
    //        var result = await _pipelineService.GetLatestProductVideoByProductCodeAsync(userId, productCode);
    //        return Ok(ApiResponse<ProductVideoDetailDto>.Success(result));
    //    }
    //    catch (KeyNotFoundException ex)
    //    {
    //        return NotFound(ApiResponse<ProductVideoDetailDto>.Fail(ex.Message, 404));
    //    }
    //    catch (Exception ex)
    //    {
    //        _logger.LogError(ex, "Error getting latest product video for product {ProductCode}", productCode);
    //        return StatusCode(500, ApiResponse<ProductVideoDetailDto>.Fail("Lỗi khi lấy video mới nhất của product", 500));
    //    }
    //}

    // Đánh dấu video là đã xóa mềm (soft delete) thay vì xóa hẳn khỏi database
    [HttpDelete("{productVideoCode}")]
    public async Task<ActionResult<ApiResponse<object>>> SoftDeleteVideo(string productVideoCode)
    {
        try
        {
            var userId = GetCurrentUserId();
            await _pipelineService.SoftDeleteProductVideoAsync(userId, productVideoCode);
            return Ok(ApiResponse<object>.Success(new { ProductVideoCode = productVideoCode }, "Xóa mềm video thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi xóa mềm video {ProductVideoCode}", productVideoCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa mềm video", 500));
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
