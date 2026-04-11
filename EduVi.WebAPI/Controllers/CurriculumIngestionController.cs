using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.CurriculumIngestion;
using EduVi.Services.CurriculumIngestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Curriculum Ingestion — Admin upload tài liệu chương trình giáo dục (.docx)
/// để Python worker parse và đưa vào Neo4j.
/// </summary>
[ApiController]
[Route("api/curriculum-ingestion")]
[Authorize]
public class CurriculumIngestionController : ControllerBase
{
    private readonly ICurriculumIngestionService _curriculumIngestionService;
    private readonly ILogger<CurriculumIngestionController> _logger;

    public CurriculumIngestionController(
        ICurriculumIngestionService curriculumIngestionService,
        ILogger<CurriculumIngestionController> logger)
    {
        _curriculumIngestionService = curriculumIngestionService;
        _logger = logger;
    }

    /// <summary>
    /// Upload file .docx chương trình giáo dục → lưu GCS → gửi RabbitMQ → Python worker xử lý.
    /// Trả về 202 Accepted kèm thông tin document để frontend polling status.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<CurriculumDocumentResponseDto>>> UploadCurriculumDocument(
        [FromForm] UploadCurriculumDocumentRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _curriculumIngestionService.UploadCurriculumDocumentAsync(userId, request);
            return StatusCode(202, ApiResponse<CurriculumDocumentResponseDto>.Success(result, "Tài liệu đã được upload và đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<CurriculumDocumentResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading curriculum document");
            return StatusCode(500, ApiResponse<CurriculumDocumentResponseDto>.Fail("Lỗi khi upload tài liệu chương trình", 500));
        }
    }

    /// <summary>
    /// Danh sách tất cả curriculum documents + trạng thái (admin dashboard table)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<CurriculumDocumentResponseDto>>>> GetAllCurriculumDocuments()
    {
        try
        {
            var result = await _curriculumIngestionService.GetAllCurriculumDocumentsAsync();
            return Ok(ApiResponse<List<CurriculumDocumentResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting curriculum documents");
            return StatusCode(500, ApiResponse<List<CurriculumDocumentResponseDto>>.Fail("Lỗi khi lấy danh sách tài liệu chương trình", 500));
        }
    }

    /// <summary>
    /// Xem chi tiết một curriculum document theo DocumentCode (frontend polls this after upload)
    /// </summary>
    [HttpGet("{documentCode}")]
    public async Task<ActionResult<ApiResponse<CurriculumDocumentResponseDto>>> GetCurriculumDocumentByCode(string documentCode)
    {
        try
        {
            var result = await _curriculumIngestionService.GetCurriculumDocumentByCodeAsync(documentCode);
            return Ok(ApiResponse<CurriculumDocumentResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<CurriculumDocumentResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting curriculum document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<CurriculumDocumentResponseDto>.Fail("Lỗi khi lấy thông tin tài liệu chương trình", 500));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("User ID not found in token");
        return userId;
    }

    /// <summary>
    /// Xóa dữ liệu curriculum khỏi Neo4j — gửi deletion task vào RabbitMQ.
    /// DB record được giữ lại cho mục đích audit.
    /// </summary>
    [HttpDelete("{documentCode}/neo4j")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteCurriculumNeo4j(string documentCode)
    {
        try
        {
            await _curriculumIngestionService.DeleteCurriculumNeo4jAsync(documentCode);
            return Accepted(ApiResponse<object>.Success(null, "Yêu cầu xóa dữ liệu Neo4j đã được gửi"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting curriculum Neo4j data for document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa dữ liệu chương trình khỏi Neo4j", 500));
        }
    }
}
