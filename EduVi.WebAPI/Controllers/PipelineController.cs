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
    private readonly IInputDocumentService _inputDocumentService;
    private readonly ILogger<PipelineController> _logger;

    public PipelineController(
        IPipelineService pipelineService,
        IInputDocumentService inputDocumentService,
        ILogger<PipelineController> logger)
    {
        _pipelineService = pipelineService;
        _inputDocumentService = inputDocumentService;
        _logger = logger;
    }

    // =====================================================================
    // INPUT DOCUMENTS
    // =====================================================================

    /// <summary>
    /// Upload file bài giảng → lưu vào GCS → lưu metadata vào DB (InputDocuments)
    /// </summary>
    [HttpPost("input-documents")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InputDocumentResponseDto>>> UploadInputDocument(
        [FromForm] UploadInputDocumentRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _inputDocumentService.UploadInputDocumentAsync(userId, request);
            return Ok(ApiResponse<InputDocumentResponseDto>.Success(result, "Upload tài liệu thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<InputDocumentResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading input document");
            return StatusCode(500, ApiResponse<InputDocumentResponseDto>.Fail("Lỗi khi upload tài liệu", 500));
        }
    }

    /// <summary>
    /// Lấy danh sách InputDocuments của Teacher hiện tại
    /// </summary>
    [HttpGet("input-documents")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<InputDocumentResponseDto>>>> GetMyInputDocuments()
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _inputDocumentService.GetInputDocumentsByTeacherAsync(userId);
            return Ok(ApiResponse<List<InputDocumentResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting input documents");
            return StatusCode(500, ApiResponse<List<InputDocumentResponseDto>>.Fail("Lỗi khi lấy danh sách tài liệu", 500));
        }
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
