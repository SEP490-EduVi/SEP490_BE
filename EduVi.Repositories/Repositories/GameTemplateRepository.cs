using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class GameTemplateRepository : IGameTemplateRepository
{
    private readonly EduViContext _context;

    public GameTemplateRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<GameTemplates?> GetTemplateByCodeAsync(string templateCode)
    {
        return await _context.GameTemplates
            .FirstOrDefaultAsync(template => template.TemplateCode == templateCode);
    }
}
