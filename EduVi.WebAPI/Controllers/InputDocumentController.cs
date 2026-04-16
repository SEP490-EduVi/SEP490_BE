using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Pipeline;
using EduVi.Services.Pipeline;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class InputDocumentController : ControllerBase
{
    private readonly IInputDocumentService _inputDocumentService;
    private readonly ILogger<InputDocumentController> _logger;

    public InputDocumentController(
        IInputDocumentService inputDocumentService,
        ILogger<InputDocumentController> logger)
    {
        _inputDocumentService = inputDocumentService;
        _logger = logger;
    }

    /// <summary>
    /// Upload InputDocument lên GCS và lưu metadata vào hệ thống
    /// </summary>
    [HttpPost]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InputDocumentResponseDto>>> UploadInputDocument(
        [FromForm] UploadInputDocumentRequestDto request)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _inputDocumentService.UploadInputDocumentAsync(teacherId, request);
            return Ok(ApiResponse<InputDocumentResponseDto>.Success(result, "Tải lên tài liệu thành công"));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<InputDocumentResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error uploading input document");
            return StatusCode(500, ApiResponse<InputDocumentResponseDto>.Fail("Lỗi khi tải lên tài liệu", 500));
        }
    }

    /// <summary>
    /// Lấy tất cả InputDocuments của Teacher hiện tại
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<InputDocumentResponseDto>>>> GetMyInputDocuments()
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _inputDocumentService.GetInputDocumentsByTeacherAsync(teacherId);
            return Ok(ApiResponse<List<InputDocumentResponseDto>>.Success(result));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error getting input documents");
            return StatusCode(500, ApiResponse<List<InputDocumentResponseDto>>.Fail("Lỗi khi lấy danh sách tài liệu", 500));
        }
    }

    /// <summary>
    /// Lấy tất cả InputDocuments theo ProjectCode của Teacher hiện tại
    /// </summary>
    [HttpGet("project/{projectCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<List<InputDocumentResponseDto>>>> GetInputDocumentsByProjectCode(string projectCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _inputDocumentService.GetInputDocumentsByProjectCodeAsync(teacherId, projectCode);
            return Ok(ApiResponse<List<InputDocumentResponseDto>>.Success(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<List<InputDocumentResponseDto>>.Fail(exception.Message, 404));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error getting input documents by project code {ProjectCode}", projectCode);
            return StatusCode(500, ApiResponse<List<InputDocumentResponseDto>>.Fail("Lỗi khi lấy danh sách tài liệu theo dự án", 500));
        }
    }

    /// <summary>
    /// Lấy chi tiết InputDocument theo DocumentCode
    /// </summary>
    [HttpGet("{documentCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<InputDocumentResponseDto>>> GetInputDocumentByCode(string documentCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            var result = await _inputDocumentService.GetInputDocumentByCodeAsync(teacherId, documentCode);
            return Ok(ApiResponse<InputDocumentResponseDto>.Success(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<InputDocumentResponseDto>.Fail(exception.Message, 404));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error getting input document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<InputDocumentResponseDto>.Fail("Lỗi khi lấy thông tin tài liệu", 500));
        }
    }

    /// <summary>
    /// Xóa cứng InputDocument khỏi DB và đồng thời xóa file trên GCS
    /// </summary>
    [HttpDelete("{documentCode}")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<string>>> DeleteInputDocument(string documentCode)
    {
        try
        {
            var teacherId = GetCurrentUserId();
            await _inputDocumentService.DeleteInputDocumentAsync(teacherId, documentCode);
            return Ok(ApiResponse<string>.Success("Đã xóa", "Xóa tài liệu thành công"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<string>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<string>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Error deleting input document {DocumentCode}", documentCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi xóa tài liệu", 500));
        }
    }

    private int GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            throw new UnauthorizedAccessException("Không tìm thấy người dùng");
        return userId;
    }
}
