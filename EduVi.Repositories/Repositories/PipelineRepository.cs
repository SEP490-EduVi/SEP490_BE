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
            .Where(project => project.TeacherId == teacherId)
            .OrderByDescending(project => project.ProjectId)
            .ToListAsync();
    }

    public async Task<Projects?> GetProjectByCodeAsync(string projectCode, bool includeRelations = false)
    {
        var query = _context.Projects.AsQueryable();

        if (includeRelations)
            query = query.Include(project => project.Products);

        return await query.FirstOrDefaultAsync(project => project.ProjectCode == projectCode);
    }

    public async Task<Projects?> GetProjectByCodeAndTeacherAsync(string projectCode, int teacherId)
    {
        return await _context.Projects
            .FirstOrDefaultAsync(project => project.ProjectCode == projectCode && project.TeacherId == teacherId);
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
            .FirstOrDefaultAsync(product => product.ProjectId == projectId && product.SourceInputId == sourceInputId);
    }

    public async Task<List<Products>> GetProductsByTeacherAsync(int teacherId)
    {
        return await _context.Products
            .Where(product => product.TeacherId == teacherId && product.Status != 7) // exclude Deleted
            .OrderByDescending(product => product.ProductId)
            .ToListAsync();
    }

    public async Task<List<Products>> GetProductsByTeacherAndProjectAsync(int teacherId, int projectId)
    {
        return await _context.Products
            .Where(product =>
                product.TeacherId == teacherId &&
                product.ProjectId == projectId &&
                product.Status != 7) // exclude Deleted
            .OrderByDescending(product => product.ProductId)
            .ToListAsync();
    }

    public async Task<Products> CreateProductAsync(Products product)
    {
        var entry = await _context.Products.AddAsync(product);
        return entry.Entity;
    }

    public async Task<List<ProductComponent>> GetProductComponentsAsync(int productId)
    {
        return await _context.ProductComponent
            .Where(productComponent => productComponent.ProductId == productId)
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
            .FirstOrDefaultAsync(material => material.MaterialCode == materialCode);
        return material?.MaterialId;
    }

    public async Task<bool> IsTeacherOwnsMaterialAsync(int teacherId, int materialId)
    {
        return await _context.TeacherMaterials
            .AnyAsync(teacherMaterial => teacherMaterial.TeacherId == teacherId && teacherMaterial.MaterialId == materialId);
    }

    public async Task<ProductVideos> CreateProductVideoAsync(ProductVideos productVideo)
    {
        var entry = await _context.ProductVideos.AddAsync(productVideo);
        return entry.Entity;
    }

    public async Task<ProductVideos?> GetLatestProductVideoAsync(int productId)
    {
        return await _context.ProductVideos
            .Where(productVideo => productVideo.ProductId == productId)
            .OrderByDescending(productVideo => productVideo.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ProductVideos?> GetProductVideoByCodeAsync(string productVideoCode)
    {
        return await _context.ProductVideos
            .FirstOrDefaultAsync(productVideo => productVideo.ProductVideoCode == productVideoCode);
    }

    public async Task<ProductVideos?> GetProductVideoByCodeAndTeacherAsync(string productVideoCode, int teacherId)
    {
        return await _context.ProductVideos
            .Include(productVideo => productVideo.Product)
            .FirstOrDefaultAsync(productVideo =>
                productVideo.ProductVideoCode == productVideoCode
                && productVideo.Product.TeacherId == teacherId);
    }

    public async Task<ProductVideos?> GetLatestActiveProductVideoAsync(int productId)
    {
        return await _context.ProductVideos
            .Where(productVideo => productVideo.ProductId == productId && productVideo.Status != "deleted")
            .OrderByDescending(productVideo => productVideo.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public async Task<ProductVideos?> GetLatestActiveProductVideoByProjectCodeAndTeacherAsync(string projectCode, int teacherId)
    {
        return await _context.ProductVideos
            .Include(productVideo => productVideo.Product)
            .Where(productVideo =>
                productVideo.Status != "deleted"
                && productVideo.Product.Project.ProjectCode == projectCode
                && productVideo.Product.TeacherId == teacherId)
            .OrderByDescending(productVideo => productVideo.CreatedAt)
            .FirstOrDefaultAsync();
    }

    public void UpdateProductVideo(ProductVideos productVideo)
    {
        _context.ProductVideos.Update(productVideo);
    }

    public async Task<Products?> GetProductByIdAsync(int productId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(product => product.ProductId == productId);
    }

    public async Task<Products?> GetProductByCodeAndTeacherAsync(string productCode, int teacherId)
    {
        return await _context.Products
            .FirstOrDefaultAsync(product => product.ProductCode == productCode && product.TeacherId == teacherId);
    }

    public void UpdateProduct(Products product)
    {
        _context.Products.Update(product);
    }

    public void DeleteProduct(Products product)
    {
        _context.Products.Remove(product);
    }
}
