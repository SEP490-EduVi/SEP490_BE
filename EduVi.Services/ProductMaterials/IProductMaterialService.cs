using EduVi.Contracts.DTOs.ProductMaterial;

namespace EduVi.Services.ProductMaterials;

public interface IProductMaterialService
{
    Task<ProductMaterialResponseDto> CreateProductMaterialAsync(int teacherId, string productCode, CreateProductMaterialRequestDto request);

    Task<List<ProductMaterialResponseDto>> GetProductMaterialsAsync(int teacherId, string productCode);

    Task<ProductMaterialResponseDto> GetProductMaterialAsync(int teacherId, string productCode, string productMaterialCode);

    Task<ProductMaterialResponseDto> UpdateProductMaterialAsync(int teacherId, string productCode, string productMaterialCode, UpdateProductMaterialRequestDto request);

    Task DeleteProductMaterialAsync(int teacherId, string productCode, string productMaterialCode);
}
