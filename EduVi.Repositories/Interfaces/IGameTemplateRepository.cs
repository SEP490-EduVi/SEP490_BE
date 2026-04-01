using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IGameTemplateRepository
{
    Task<GameTemplates?> GetTemplateByCodeAsync(string templateCode);
}
