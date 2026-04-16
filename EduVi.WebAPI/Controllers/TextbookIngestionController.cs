using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.TextbookIngestion;
using EduVi.Services.TextbookIngestion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

/// <summary>
/// Textbook Ingestion — Admin upload sách giáo khoa (.pdf) để Python worker
/// parse và đưa vào Neo4j.
/// </summary>
[ApiController]
[Route("api/textbook-ingestion")]
[Authorize]
public class TextbookIngestionController : ControllerBase
{
    private readonly ITextbookIngestionService _textbookIngestionService;
    private readonly ILogger<TextbookIngestionController> _logger;

    public TextbookIngestionController(
        ITextbookIngestionService textbookIngestionService,
        ILogger<TextbookIngestionController> logger)
    {
        _textbookIngestionService = textbookIngestionService;
        _logger = logger;
    }

    /// <summary>
    /// Upload file .pdf sách giáo khoa → lưu GCS → gửi RabbitMQ → Python worker xử lý.
    /// Trả về 202 Accepted kèm thông tin document để frontend polling status.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<TextbookDocumentResponseDto>>> UploadTextbookDocument(
        [FromForm] UploadTextbookDocumentRequestDto request)
    {
        try
        {
            var userId = GetCurrentUserId();
            var result = await _textbookIngestionService.UploadTextbookDocumentAsync(userId, request);
            return StatusCode(202, ApiResponse<TextbookDocumentResponseDto>.Success(result, "Sách giáo khoa đã được tải lên và đưa vào hàng đợi xử lý"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<TextbookDocumentResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading textbook document");
            return StatusCode(500, ApiResponse<TextbookDocumentResponseDto>.Fail("Lỗi khi tải lên sách giáo khoa", 500));
        }
    }

    /// <summary>
    /// Danh sách tất cả textbook documents + trạng thái (admin dashboard table)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<TextbookDocumentResponseDto>>>> GetAllTextbookDocuments()
    {
        try
        {
            var result = await _textbookIngestionService.GetAllTextbookDocumentsAsync();
            return Ok(ApiResponse<List<TextbookDocumentResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting textbook documents");
            return StatusCode(500, ApiResponse<List<TextbookDocumentResponseDto>>.Fail("Lỗi khi lấy danh sách sách giáo khoa", 500));
        }
    }

    /// <summary>
    /// Xem chi tiết một textbook document theo DocumentCode (frontend polls this after upload)
    /// </summary>
    [HttpGet("{documentCode}")]
    public async Task<ActionResult<ApiResponse<TextbookDocumentResponseDto>>> GetTextbookDocumentByCode(string documentCode)
    {
        try
        {
            var result = await _textbookIngestionService.GetTextbookDocumentByCodeAsync(documentCode);
            return Ok(ApiResponse<TextbookDocumentResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<TextbookDocumentResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting textbook document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<TextbookDocumentResponseDto>.Fail("Lỗi khi lấy thông tin sách giáo khoa", 500));
        }
    }

    /// <summary>
    /// Xóa dữ liệu textbook khỏi Neo4j — gửi deletion task vào RabbitMQ.
    /// DB record được giữ lại cho mục đích audit.
    /// </summary>
    [HttpDelete("{documentCode}/neo4j")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTextbookNeo4j(string documentCode)
    {
        try
        {
            await _textbookIngestionService.DeleteTextbookNeo4jAsync(documentCode);
            return Accepted(ApiResponse<object>.Success(null, "Yêu cầu xóa dữ liệu Neo4j đã được gửi"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<object>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting textbook Neo4j data for document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa dữ liệu sách giáo khoa khỏi Neo4j", 500));
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
