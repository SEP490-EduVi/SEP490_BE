using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.Project;
using EduVi.Services.Project;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace EduVi.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectController : ControllerBase
{
    private readonly IProjectService _projectService;
    private readonly ILogger<ProjectController> _logger;

    public ProjectController(IProjectService projectService, ILogger<ProjectController> logger)
    {
        _projectService = projectService;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<List<ProjectResponseDto>>>> GetMyProjects()
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _projectService.GetProjectsByTeacherAsync(teacherId);
            return Ok(ApiResponse<List<ProjectResponseDto>>.Success(result));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting projects");
            return StatusCode(500, ApiResponse<List<ProjectResponseDto>>.Fail("Lỗi khi lấy danh sách project", 500));
        }
    }

    [HttpGet("{projectCode}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> GetByCode(string projectCode)
    {
        try
        {
            var result = await _projectService.GetProjectByCodeAsync(projectCode);
            return Ok(ApiResponse<ProjectResponseDto>.Success(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectResponseDto>.Fail(ex.Message, 404));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting project {ProjectCode}", projectCode);
            return StatusCode(500, ApiResponse<ProjectResponseDto>.Fail("Lỗi khi lấy project", 500));
        }
    }

    [HttpPost]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> Create(
        [FromBody] CreateProjectRequestDto request)
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _projectService.CreateProjectAsync(teacherId, request);
            return Ok(ApiResponse<ProjectResponseDto>.Success(result, "Tạo project thành công"));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ProjectResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating project");
            return StatusCode(500, ApiResponse<ProjectResponseDto>.Fail("Lỗi khi tạo project", 500));
        }
    }

    [HttpPut("{projectCode}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<ProjectResponseDto>>> Update(
        string projectCode, [FromBody] UpdateProjectRequestDto request)
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var result = await _projectService.UpdateProjectAsync(teacherId, projectCode, request);
            return Ok(ApiResponse<ProjectResponseDto>.Success(result, "Cập nhật project thành công"));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<ProjectResponseDto>.Fail(ex.Message, 404));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<ProjectResponseDto>.Fail(ex.Message, 400));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating project {ProjectCode}", projectCode);
            return StatusCode(500, ApiResponse<ProjectResponseDto>.Fail("Lỗi khi cập nhật project", 500));
        }
    }

    [HttpDelete("{projectCode}")]
    [Authorize(Roles = "Teacher")]
    public async Task<ActionResult<ApiResponse<string>>> Delete(string projectCode)
    {
        try
        {
            var teacherId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _projectService.DeleteProjectAsync(teacherId, projectCode);
            return Ok(ApiResponse<string>.Success("Đã xóa", "Xóa project thành công"));
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
            _logger.LogError(ex, "Error deleting project {ProjectCode}", projectCode);
            return StatusCode(500, ApiResponse<string>.Fail("Lỗi khi xóa project", 500));
        }
    }
}
