using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Curriculum;
using EduVi.Services.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LessonController : ControllerBase
{
    private readonly ICurriculumService _curriculumService;
    private readonly ILogger<LessonController> _logger;

    public LessonController(ICurriculumService curriculumService, ILogger<LessonController> logger)
    {
        _curriculumService = curriculumService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<LessonResponseDto>>>> GetAll(
        [FromQuery] string? subjectCode)
    {
        try
        {
            var result = await _curriculumService.GetAllLessonsAsync(subjectCode);
            return Ok(ApiResponse<List<LessonResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lessons");
            return StatusCode(500, ApiResponse<List<LessonResponseDto>>.Fail("Lỗi khi lấy danh sách bài học", 500));
        }
    }

    [HttpGet("{lessonCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LessonResponseDto>>> GetByCode(string lessonCode)
    {
        try
        {
            var result = await _curriculumService.GetLessonByCodeAsync(lessonCode);
            return Ok(ApiResponse<LessonResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<LessonResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting lesson {LessonCode}", lessonCode);
            return StatusCode(500, ApiResponse<LessonResponseDto>.Fail("Lỗi khi lấy bài học", 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<LessonResponseDto>>> Create(
        [FromBody] CreateLessonRequestDto request)
    {
        try
        {
            var result = await _curriculumService.CreateLessonAsync(request);
            return Ok(ApiResponse<LessonResponseDto>.Success(result, "Tạo bài học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<LessonResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<LessonResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating lesson");
            return StatusCode(500, ApiResponse<LessonResponseDto>.Fail("Lỗi khi tạo bài học", 500));
        }
    }

    [HttpPut("{lessonCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<LessonResponseDto>>> Update(
        string lessonCode, [FromBody] UpdateLessonRequestDto request)
    {
        try
        {
            var result = await _curriculumService.UpdateLessonAsync(lessonCode, request);
            return Ok(ApiResponse<LessonResponseDto>.Success(result, "Cập nhật bài học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<LessonResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<LessonResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating lesson {LessonCode}", lessonCode);
            return StatusCode(500, ApiResponse<LessonResponseDto>.Fail("Lỗi khi cập nhật bài học", 500));
        }
    }

    [HttpDelete("{lessonCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string lessonCode)
    {
        try
        {
            await _curriculumService.DeleteLessonAsync(lessonCode);
            return Ok(ApiResponse<string>.Success("Đã xóa", "Xóa bài học thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting lesson {LessonCode}", lessonCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi xóa bài học", 500));
        }
    }
}
