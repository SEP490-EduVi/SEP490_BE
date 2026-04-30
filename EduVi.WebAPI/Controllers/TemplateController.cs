using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Template;
using EduVi.Services.Template;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TemplateController : ControllerBase
{
    private readonly ITemplateService _templateService;
    private readonly ILogger<TemplateController> _logger;

    public TemplateController(ITemplateService templateService, ILogger<TemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<TemplateResponseDto>>>> GetTemplates()
    {
        try
        {
            var result = await _templateService.GetAllTemplatesAsync();
            return Ok(ApiResponse<List<TemplateResponseDto>>.Success(result));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Template API failure: cannot retrieve template list");
            return StatusCode(500, ApiResponse<List<TemplateResponseDto>>.Fail("Lỗi khi lấy danh sách template", 500));
        }
    }

    [HttpGet("{templateCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> GetTemplateByCode(string templateCode)
    {
        try
        {
            var result = await _templateService.GetTemplateByCodeAsync(templateCode);
            return Ok(ApiResponse<TemplateResponseDto>.Success(result));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<TemplateResponseDto>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<TemplateResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Template API failure: cannot retrieve template {TemplateCode}", templateCode);
            return StatusCode(500, ApiResponse<TemplateResponseDto>.Fail("Lỗi khi lấy chi tiết template", 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> CreateTemplate([FromBody] CreateTemplateRequestDto request)
    {
        try
        {
            var result = await _templateService.CreateTemplateAsync(request);
            return CreatedAtAction(nameof(GetTemplateByCode), new { templateCode = result.TemplateCode },
                ApiResponse<TemplateResponseDto>.Success(result, "Tạo template thành công", 201));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<TemplateResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Template API failure: cannot create template with name {TemplateName}", request.Name);
            return StatusCode(500, ApiResponse<TemplateResponseDto>.Fail("Lỗi khi tạo template", 500));
        }
    }

    [HttpPut("{templateCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<TemplateResponseDto>>> UpdateTemplate(
        string templateCode,
        [FromBody] UpdateTemplateRequestDto request)
    {
        try
        {
            var result = await _templateService.UpdateTemplateAsync(templateCode, request);
            return Ok(ApiResponse<TemplateResponseDto>.Success(result, "Cập nhật template thành công"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<TemplateResponseDto>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<TemplateResponseDto>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Template API failure: cannot update template {TemplateCode}", templateCode);
            return StatusCode(500, ApiResponse<TemplateResponseDto>.Fail("Lỗi khi cập nhật template", 500));
        }
    }

    [HttpDelete("{templateCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<object>>> DeleteTemplate(string templateCode)
    {
        try
        {
            await _templateService.DeleteTemplateAsync(templateCode);
            return Ok(ApiResponse<object>.Success(null, "Xóa template thành công"));
        }
        catch (KeyNotFoundException exception)
        {
            return NotFound(ApiResponse<object>.Fail(exception.Message, 404));
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(ApiResponse<object>.Fail(exception.Message, 400));
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Template API failure: cannot delete template {TemplateCode}", templateCode);
            return StatusCode(500, ApiResponse<object>.Fail("Lỗi khi xóa template", 500));
        }
    }
}
