using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface ITemplateRepository
{
    Task<List<CardTemplates>> GetAllTemplatesAsync();
    Task<CardTemplates?> GetTemplateByCodeAsync(string templateCode);
    Task<CardTemplates> CreateTemplateAsync(CardTemplates template);
    void UpdateTemplate(CardTemplates template);
    void DeleteTemplate(CardTemplates template);
}
