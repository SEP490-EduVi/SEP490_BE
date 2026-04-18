using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IProductMaterialRepository
{
    Task<List<ProductMaterials>> GetProductMaterialsByProductIdAsync(int productId);

    Task<ProductMaterials?> GetProductMaterialByCodeAndProductIdAsync(string productMaterialCode, int productId);

    Task<bool> ExistsMarketplaceMaterialInProductAsync(int productId, int materialId);

    Task<ProductMaterials> CreateProductMaterialAsync(ProductMaterials productMaterial);

    void UpdateProductMaterial(ProductMaterials productMaterial);

    void DeleteProductMaterial(ProductMaterials productMaterial);
}
