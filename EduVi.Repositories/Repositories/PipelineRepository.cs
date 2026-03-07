using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class PipelineRepository : IPipelineRepository
{
    private readonly EduViContext _context;

    public PipelineRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<InputDocuments?> GetInputDocumentByIdAsync(int documentId)
    {
        return await _context.InputDocuments
            .Include(d => d.Subject)
            .Include(d => d.Grade)
            .Include(d => d.Lesson)
            .FirstOrDefaultAsync(d => d.DocumentId == documentId);
    }

    public async Task<InputDocuments?> GetInputDocumentByCodeAsync(string documentCode)
    {
        return await _context.InputDocuments
            .Include(d => d.Subject)
            .Include(d => d.Grade)
            .Include(d => d.Lesson)
            .FirstOrDefaultAsync(d => d.DocumentCode == documentCode);
    }

    public async Task<InputDocuments?> GetExistingInputDocumentAsync(int teacherId, int subjectId, int gradeId, int? lessonId)
    {
        return await _context.InputDocuments
            .Include(d => d.Subject)
            .Include(d => d.Grade)
            .Include(d => d.Lesson)
            .FirstOrDefaultAsync(d =>
                d.TeacherId == teacherId &&
                d.SubjectId == subjectId &&
                d.GradeId == gradeId &&
                d.LessonId == lessonId);
    }

    public async Task<InputDocuments> CreateInputDocumentAsync(InputDocuments document)
    {
        var entry = await _context.InputDocuments.AddAsync(document);
        return entry.Entity;
    }

    public void UpdateInputDocument(InputDocuments document)
    {
        _context.InputDocuments.Update(document);
    }

    public async Task<List<InputDocuments>> GetInputDocumentsByTeacherAsync(int teacherId)
    {
        return await _context.InputDocuments
            .Include(d => d.Subject)
            .Include(d => d.Grade)
            .Include(d => d.Lesson)
            .Where(d => d.TeacherId == teacherId)
            .OrderByDescending(d => d.UploadDate)
            .ToListAsync();
    }

    public async Task<List<Projects>> GetProjectsByTeacherAsync(int teacherId)
    {
        return await _context.Projects
            .Where(p => p.TeacherId == teacherId)
            .OrderByDescending(p => p.ProjectId)
            .ToListAsync();
    }

    public async Task<Projects?> GetProjectByCodeAsync(string projectCode, bool includeRelations = false)
    {
        var query = _context.Projects.AsQueryable();

        if (includeRelations)
            query = query.Include(p => p.Products);

        return await query.FirstOrDefaultAsync(p => p.ProjectCode == projectCode);
    }

    public async Task<Projects?> GetProjectByCodeAndTeacherAsync(string projectCode, int teacherId)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(p => p.ProjectCode == projectCode && p.TeacherId == teacherId);
    }

    public async Task<Projects> CreateProjectAsync(Projects project)
    {
        var entry = await _context.Projects.AddAsync(project);
        return entry.Entity;
    }

    public void UpdateProject(Projects project)
    {
        _context.Projects.Update(project);
    }

    public void DeleteProject(Projects project)
    {
        _context.Projects.Remove(project);
    }

    public async Task<Products?> GetExistingProductAsync(int projectId, int sourceInputId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ProjectId == projectId && p.SourceInputId == sourceInputId);
    }

    public async Task<Products> CreateProductAsync(Products product)
    {
        var entry = await _context.Products.AddAsync(product);
        return entry.Entity;
    }

    public async Task<List<ProductComponent>> GetProductComponentsAsync(int productId)
    {
        return await _context.ProductComponent
            .Where(pc => pc.ProductId == productId)
            .ToListAsync();
    }

    public void DeleteProductComponents(List<ProductComponent> components)
    {
        _context.ProductComponent.RemoveRange(components);
    }

    public async Task AddProductComponentsAsync(List<ProductComponent> components)
    {
        await _context.ProductComponent.AddRangeAsync(components);
    }

    public async Task<int?> GetMaterialIdByCodeAsync(string materialCode)
    {
        var material = await _context.Materials
            .FirstOrDefaultAsync(m => m.MaterialCode == materialCode);
        return material?.MaterialId;
    }

    public async Task<bool> IsTeacherOwnsMaterialAsync(int teacherId, int materialId)
    {
        return await _context.TeacherMaterials
            .AnyAsync(tm => tm.TeacherId == teacherId && tm.MaterialId == materialId);
    }

    public async Task<Products?> GetProductByIdAsync(int productId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ProductId == productId);
    }

    public async Task<Products?> GetProductByCodeAndTeacherAsync(string productCode, int teacherId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(p => p.ProductCode == productCode && p.TeacherId == teacherId);
    }

    public void UpdateProduct(Products product)
    {
        _context.Products.Update(product);
    }
}
