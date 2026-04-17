using EduVi.Contracts.DTOs.Template;

namespace EduVi.Services.Template;

public interface ITemplateService
{
    Task<List<TemplateResponseDto>> GetAllTemplatesAsync();
    Task<TemplateResponseDto> GetTemplateByCodeAsync(string templateCode);
    Task<TemplateResponseDto> CreateTemplateAsync(CreateTemplateRequestDto request);
    Task<TemplateResponseDto> UpdateTemplateAsync(string templateCode, UpdateTemplateRequestDto request);
    Task DeleteTemplateAsync(string templateCode);
}
