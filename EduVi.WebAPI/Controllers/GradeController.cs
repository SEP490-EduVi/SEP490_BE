using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Curriculum;
using EduVi.Services.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class GradeController : ControllerBase
{
    private readonly ICurriculumService _curriculumService;
    private readonly ILogger<GradeController> _logger;

    public GradeController(ICurriculumService curriculumService, ILogger<GradeController> logger)
    {
        _curriculumService = curriculumService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<GradeResponseDto>>>> GetAll()
    {
        try
        {
            var result = await _curriculumService.GetAllGradesAsync();
            return Ok(ApiResponse<List<GradeResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grades");
            return StatusCode(500, ApiResponse<List<GradeResponseDto>>.Fail("Lỗi khi lấy danh sách khối lớp", 500));
        }
    }

    [HttpGet("{gradeCode}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<GradeResponseDto>>> GetByCode(string gradeCode)
    {
        try
        {
            var result = await _curriculumService.GetGradeByCodeAsync(gradeCode);
            return Ok(ApiResponse<GradeResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GradeResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting grade {GradeCode}", gradeCode);
            return StatusCode(500, ApiResponse<GradeResponseDto>.Fail("Lỗi khi lấy khối lớp", 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<GradeResponseDto>>> Create(
        [FromBody] CreateGradeRequestDto request)
    {
        try
        {
            var result = await _curriculumService.CreateGradeAsync(request);
            return Ok(ApiResponse<GradeResponseDto>.Success(result, "Tạo khối lớp thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GradeResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating grade");
            return StatusCode(500, ApiResponse<GradeResponseDto>.Fail("Lỗi khi tạo khối lớp", 500));
        }
    }

    [HttpPut("{gradeCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<GradeResponseDto>>> Update(
        string gradeCode, [FromBody] UpdateGradeRequestDto request)
    {
        try
        {
            var result = await _curriculumService.UpdateGradeAsync(gradeCode, request);
            return Ok(ApiResponse<GradeResponseDto>.Success(result, "Cập nhật khối lớp thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<GradeResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<GradeResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating grade {GradeCode}", gradeCode);
            return StatusCode(500, ApiResponse<GradeResponseDto>.Fail("Lỗi khi cập nhật khối lớp", 500));
        }
    }

    [HttpDelete("{gradeCode}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string gradeCode)
    {
        try
        {
            await _curriculumService.DeleteGradeAsync(gradeCode);
            return Ok(ApiResponse<string>.Success("Đã xóa", "Xóa khối lớp thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting grade {GradeCode}", gradeCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi xóa khối lớp", 500));
        }
    }
}
