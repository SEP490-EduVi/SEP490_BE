using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class TemplateRepository : ITemplateRepository
{
    private readonly EduViContext _context;

    public TemplateRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<List<CardTemplates>> GetAllTemplatesAsync()
    {
        return await _context.CardTemplates
            .OrderBy(template => template.Name)
            .ToListAsync();
    }

    public async Task<CardTemplates?> GetTemplateByCodeAsync(string templateCode)
    {
        return await _context.CardTemplates
            .FirstOrDefaultAsync(template => template.TemplateCode == templateCode);
    }

    public async Task<CardTemplates> CreateTemplateAsync(CardTemplates template)
    {
        var entry = await _context.CardTemplates.AddAsync(template);
        return entry.Entity;
    }

    public void UpdateTemplate(CardTemplates template)
    {
        _context.CardTemplates.Update(template);
    }

    public void DeleteTemplate(CardTemplates template)
    {
        _context.CardTemplates.Remove(template);
    }
}
