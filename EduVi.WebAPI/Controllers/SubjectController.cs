using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Curriculum;
using EduVi.Services.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SubjectController : ControllerBase
{
    private readonly ICurriculumService _curriculumService;
    private readonly ILogger<SubjectController> _logger;

    public SubjectController(ICurriculumService curriculumService, ILogger<SubjectController> logger)
    {
        _curriculumService = curriculumService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<SubjectResponseDto>>>> GetAll()
    {
        try
        {
            var result = await _curriculumService.GetAllSubjectsAsync();
            return Ok(ApiResponse<List<SubjectResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subjects");
            return StatusCode(500, ApiResponse<List<SubjectResponseDto>>.Fail("Lỗi khi lấy danh sách môn học", 500));
        }
    }

    [HttpGet("{subjectCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SubjectResponseDto>>> GetByCode(string subjectCode)
    {
        try
        {
            var result = await _curriculumService.GetSubjectByCodeAsync(subjectCode);
            return Ok(ApiResponse<SubjectResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubjectResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subject {SubjectCode}", subjectCode);
            return StatusCode(500, ApiResponse<SubjectResponseDto>.Fail("Lỗi khi lấy môn học", 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<SubjectResponseDto>>> Create(
        [FromBody] CreateSubjectRequestDto request)
    {
        try
        {
            var result = await _curriculumService.CreateSubjectAsync(request);
            return Ok(ApiResponse<SubjectResponseDto>.Success(result, "Tạo môn học thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SubjectResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subject");
            return StatusCode(500, ApiResponse<SubjectResponseDto>.Fail("Lỗi khi tạo môn học", 500));
        }
    }

    [HttpPut("{subjectCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<SubjectResponseDto>>> Update(
        string subjectCode, [FromBody] UpdateSubjectRequestDto request)
    {
        try
        {
            var result = await _curriculumService.UpdateSubjectAsync(subjectCode, request);
            return Ok(ApiResponse<SubjectResponseDto>.Success(result, "Cập nhật môn học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<SubjectResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<SubjectResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subject {SubjectCode}", subjectCode);
            return StatusCode(500, ApiResponse<SubjectResponseDto>.Fail("Lỗi khi cập nhật môn học", 500));
        }
    }

    [HttpDelete("{subjectCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string subjectCode)
    {
        try
        {
            await _curriculumService.DeleteSubjectAsync(subjectCode);
            return Ok(ApiResponse<string>.Success("Đã xóa", "Xóa môn học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subject {SubjectCode}", subjectCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi xóa môn học", 500));
        }
    }
}
