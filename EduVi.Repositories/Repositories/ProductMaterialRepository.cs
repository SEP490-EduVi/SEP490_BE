using EduVi.Repositories.DBContext;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.Repositories;

public class ProductMaterialRepository : IProductMaterialRepository
{
    private readonly EduViContext _context;

    public ProductMaterialRepository(EduViContext context)
    {
        _context = context;
    }

    public async Task<List<ProductMaterials>> GetProductMaterialsByProductIdAsync(int productId)
    {
        return await _context.ProductMaterials
            .Include(productMaterial => productMaterial.Material)
            .Where(productMaterial => productMaterial.ProductId == productId)
            .OrderByDescending(productMaterial => productMaterial.CreatedAt)
            .ToListAsync();
    }

    public async Task<ProductMaterials?> GetProductMaterialByCodeAndProductIdAsync(string productMaterialCode, int productId)
    {
        return await _context.ProductMaterials
            .Include(productMaterial => productMaterial.Material)
            .FirstOrDefaultAsync(productMaterial =>
                productMaterial.ProductMaterialCode == productMaterialCode
                && productMaterial.ProductId == productId);
    }

    public async Task<bool> ExistsMarketplaceMaterialInProductAsync(int productId, int materialId)
    {
        return await _context.ProductMaterials
            .AnyAsync(productMaterial =>
                productMaterial.ProductId == productId
                && productMaterial.MaterialId == materialId);
    }

    public async Task<ProductMaterials> CreateProductMaterialAsync(ProductMaterials productMaterial)
    {
        var entityEntry = await _context.ProductMaterials.AddAsync(productMaterial);
        return entityEntry.Entity;
    }

    public void UpdateProductMaterial(ProductMaterials productMaterial)
    {
        _context.ProductMaterials.Update(productMaterial);
    }

    public void DeleteProductMaterial(ProductMaterials productMaterial)
    {
        _context.ProductMaterials.Remove(productMaterial);
    }
}
