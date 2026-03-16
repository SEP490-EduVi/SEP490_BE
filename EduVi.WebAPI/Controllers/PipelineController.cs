using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Services.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PipelineController : ControllerBase
{
    private readonly IPipelineService _pipelineService;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IPipelineService pipelineService,
        ILogger<PipelineController> logger)
    {
        _pipelineService = pipelineService;
        _logger = logger;
    }

    // =====================================================================
    // LESSON ANALYSIS
    // =====================================================================

    /// <summary>
    /// Chọn InputDocument đã upload → tạo Product (NEW) → gửi RabbitMQ → Python worker xử lý
    /// </summary>
    [HttpPost("lesson-analysis")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PipelineTaskResponseDto>>> AnalyzeLesson(
        [FromBody] LessonAnalysisRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.CreateLessonAnalysisTaskAsync(userId, request);
            return Ok(ApiResponse<PipelineTaskResponseDto>.Success(result, "Task đã được đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PipelineTaskResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lesson analysis task");
            return StatusCode(500, ApiResponse<PipelineTaskResponseDto>.Fail("Lỗi khi tạo task phân tích bài giảng", 500));
        }
    }

    // =====================================================================
    // SLIDE GENERATION
    // =====================================================================

    /// <summary>
    /// Trigger tạo slide presentation từ Product đã được evaluate → gửi RabbitMQ → Python worker xử lý
    /// </summary>
    [HttpPost("generate-slides")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PipelineTaskResponseDto>>> GenerateSlides(
        [FromBody] SlideGenerationRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _pipelineService.CreateSlideGenerationTaskAsync(userId, request);
            return Ok(ApiResponse<PipelineTaskResponseDto>.Success(result, "Task tạo slide đã được đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<PipelineTaskResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating slide generation task");
            return StatusCode(500, ApiResponse<PipelineTaskResponseDto>.Fail("Lỗi khi tạo task tạo slide", 500));
        }
    }

    // =====================================================================
    // SLIDE EDIT
    // =====================================================================

    /// <summary>
    /// [Teacher] Lưu bản slide đã chỉnh sửa cuối cùng vào SlideEditedDocument.
    /// Bản gốc AI generate (SlideDocument) được giữ nguyên để tham chiếu.
    /// </summary>
    [HttpPut("products/{productCode}/slide")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<object>>> SaveEditedSlide(
        string productCode,
        [FromBody] SaveEditedSlideRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var slideEditedDocumentUrl = await _pipelineService.SaveEditedSlideAsync(userId, productCode, request);
            return Ok(ApiResponse<object>.Success(new
            {
                SlideEditedDocumentUrl = slideEditedDocumentUrl
            }, "Slide đã được lưu thành công"));
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
            _logger.LogError(ex, "Error saving edited slide for product {ProductCode}", productCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi lưu slide đã chỉnh sửa", 500));
        }
    }

    // =====================================================================
    // TASK STATUS
    // =====================================================================

    /// <summary>
    /// Kiểm tra trạng thái task (fallback khi SignalR bị ngắt)
    /// </summary>
    [HttpGet("status/{taskId:guid}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<PipelineProgressDto>>> GetTaskStatus(Guid taskId)
    {
        try
        {
            var result = await _pipelineService.GetTaskStatusAsync(taskId);
            if (result is null)
                return NotFound(ApiResponse<PipelineProgressDto>.Fail("Không tìm thấy task", 404));

            return Ok(ApiResponse<PipelineProgressDto>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting task status. TaskId={TaskId}", taskId);
            return StatusCode(500, ApiResponse<PipelineProgressDto>.Fail("Lỗi khi kiểm tra trạng thái task", 500));
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
