using EduVi.Contracts.DTOs.Project;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Project;

public class ProjectService : IProjectService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProjectService> _logger;

    public ProjectService(IUnitOfWork unitOfWork, ILogger<ProjectService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<List<ProjectResponseDto>> GetProjectsByTeacherAsync(int teacherId)
    {
        var projects = await _unitOfWork.PipelineRepository.GetProjectsByTeacherAsync(teacherId);
        return projects.Select(MapToProjectResponse).ToList();
    }

    public async Task<ProjectResponseDto> GetProjectByCodeAsync(string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(projectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại");

        if (project.Status == ProjectStatusConstants.Deleted)
            throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại");

        return MapToProjectResponse(project);
    }

    public async Task<ProjectResponseDto> CreateProjectAsync(int teacherId, CreateProjectRequestDto request)
    {
        var existing = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(request.ProjectCode);
        if (existing is not null)
            throw new InvalidOperationException($"Mã dự án '{request.ProjectCode}' đã tồn tại");

        var project = new Projects
        {
            TeacherId = teacherId,
            ProjectCode = request.ProjectCode,
            ProjectName = request.ProjectName,
            Status = ProjectStatusConstants.Active,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.PipelineRepository.CreateProjectAsync(project);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(project.ProjectCode, includeRelations: true);
        return MapToProjectResponse(saved!);
    }

    public async Task<ProjectResponseDto> UpdateProjectAsync(int teacherId, string projectCode, UpdateProjectRequestDto request)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAndTeacherAsync(projectCode, teacherId)
            ?? throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại hoặc không thuộc về bạn");

        if (request.ProjectCode is not null && request.ProjectCode != projectCode)
        {
            var existing = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(request.ProjectCode);
            if (existing is not null)
                throw new InvalidOperationException($"Mã dự án '{request.ProjectCode}' đã được sử dụng");
            project.ProjectCode = request.ProjectCode;
        }

        if (request.ProjectName is not null)
            project.ProjectName = request.ProjectName;

        if (request.Status.HasValue)
            project.Status = request.Status.Value;

        _unitOfWork.PipelineRepository.UpdateProject(project);
        await _unitOfWork.SaveChangesAsync();

        var saved = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(project.ProjectCode, includeRelations: true);
        return MapToProjectResponse(saved!);
    }

    public async Task DeleteProjectAsync(int teacherId, string projectCode)
    {
        var project = await _unitOfWork.PipelineRepository.GetProjectByCodeAsync(projectCode, includeRelations: true)
            ?? throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại");

        if (project.TeacherId != teacherId)
            throw new InvalidOperationException("Dự án không thuộc về bạn");

        if (project.Products.Count > 0)
            throw new InvalidOperationException($"Không thể xóa Dự án đang có {project.Products.Count} Product");

        if (project.Status == ProjectStatusConstants.Deleted)
            throw new KeyNotFoundException($"Dự án '{projectCode}' không tồn tại");

        project.Status = ProjectStatusConstants.Deleted;
        _unitOfWork.PipelineRepository.UpdateProject(project);
        await _unitOfWork.SaveChangesAsync();
    }

    private static ProjectResponseDto MapToProjectResponse(Projects project)
    {
        return new ProjectResponseDto
        {
            ProjectCode = project.ProjectCode,
            ProjectName = project.ProjectName,
            Status = project.Status,
            CreatedAt = project.CreatedAt
        };
    }
}
