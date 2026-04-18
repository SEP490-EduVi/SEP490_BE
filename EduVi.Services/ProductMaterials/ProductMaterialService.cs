using EduVi.Contracts.Common;
using EduVi.Contracts.DTOs.ProductMaterial;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using ProductMaterialEntity = EduVi.Repositories.Models.ProductMaterials;

namespace EduVi.Services.ProductMaterials;

public class ProductMaterialService : IProductMaterialService
{
    private const string MarketplaceSourceType = "Marketplace";
    private const string UploadSourceType = "Upload";

    private readonly IUnitOfWork _unitOfWork;

    public ProductMaterialService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductMaterialResponseDto> CreateProductMaterialAsync(int teacherId, string productCode, CreateProductMaterialRequestDto request)
    {
        var product = await GetTeacherProductAsync(teacherId, productCode);
        var normalizedSourceType = NormalizeSourceType(request.SourceType);

        ProductMaterialEntity productMaterial;
        if (normalizedSourceType == MarketplaceSourceType)
        {
            productMaterial = await BuildMarketplaceProductMaterialAsync(product, teacherId, request);
        }
        else
        {
            productMaterial = BuildUploadedProductMaterial(product, request);
        }

        await _unitOfWork.ProductMaterialRepository.CreateProductMaterialAsync(productMaterial);
        await _unitOfWork.SaveChangesAsync();

        var savedProductMaterial = await _unitOfWork.ProductMaterialRepository
            .GetProductMaterialByCodeAndProductIdAsync(productMaterial.ProductMaterialCode, product.ProductId)
            ?? throw new InvalidOperationException("Không thể tải lại học liệu sản phẩm vừa tạo");

        return MapToResponse(savedProductMaterial, product.ProductCode);
    }

    public async Task<List<ProductMaterialResponseDto>> GetProductMaterialsAsync(int teacherId, string productCode)
    {
        var product = await GetTeacherProductAsync(teacherId, productCode);
        var productMaterials = await _unitOfWork.ProductMaterialRepository.GetProductMaterialsByProductIdAsync(product.ProductId);
        return productMaterials.Select(productMaterial => MapToResponse(productMaterial, product.ProductCode)).ToList();
    }

    public async Task<ProductMaterialResponseDto> GetProductMaterialAsync(int teacherId, string productCode, string productMaterialCode)
    {
        var product = await GetTeacherProductAsync(teacherId, productCode);
        var productMaterial = await _unitOfWork.ProductMaterialRepository
            .GetProductMaterialByCodeAndProductIdAsync(productMaterialCode, product.ProductId)
            ?? throw new KeyNotFoundException($"Học liệu sản phẩm '{productMaterialCode}' không tồn tại");

        return MapToResponse(productMaterial, product.ProductCode);
    }

    public async Task<ProductMaterialResponseDto> UpdateProductMaterialAsync(
        int teacherId,
        string productCode,
        string productMaterialCode,
        UpdateProductMaterialRequestDto request)
    {
        var product = await GetTeacherProductAsync(teacherId, productCode);
        var productMaterial = await _unitOfWork.ProductMaterialRepository
            .GetProductMaterialByCodeAndProductIdAsync(productMaterialCode, product.ProductId)
            ?? throw new KeyNotFoundException($"Học liệu sản phẩm '{productMaterialCode}' không tồn tại");

        if (productMaterial.SourceType == MarketplaceSourceType)
            throw new InvalidOperationException("Học liệu từ marketplace không thể cập nhật trực tiếp trong sản phẩm");

        if (!string.IsNullOrWhiteSpace(request.Title))
            productMaterial.Title = request.Title.Trim();

        if (!string.IsNullOrWhiteSpace(request.Type))
            productMaterial.Type = request.Type.Trim();

        if (!string.IsNullOrWhiteSpace(request.ResourceUrl))
            productMaterial.ResourceUrl = request.ResourceUrl.Trim();

        if (request.PreviewUrl is not null)
            productMaterial.PreviewUrl = string.IsNullOrWhiteSpace(request.PreviewUrl)
                ? null
                : request.PreviewUrl.Trim();

        productMaterial.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.ProductMaterialRepository.UpdateProductMaterial(productMaterial);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponse(productMaterial, product.ProductCode);
    }

    public async Task DeleteProductMaterialAsync(int teacherId, string productCode, string productMaterialCode)
    {
        var product = await GetTeacherProductAsync(teacherId, productCode);
        var productMaterial = await _unitOfWork.ProductMaterialRepository
            .GetProductMaterialByCodeAndProductIdAsync(productMaterialCode, product.ProductId)
            ?? throw new KeyNotFoundException($"Học liệu sản phẩm '{productMaterialCode}' không tồn tại");

        _unitOfWork.ProductMaterialRepository.DeleteProductMaterial(productMaterial);
        await _unitOfWork.SaveChangesAsync();
    }

    private async Task<Products> GetTeacherProductAsync(int teacherId, string productCode)
    {
        var product = await _unitOfWork.PipelineRepository.GetProductByCodeAndTeacherAsync(productCode, teacherId)
            ?? throw new KeyNotFoundException($"Nội dung số '{productCode}' không tồn tại hoặc không thuộc về bạn");

        if (product.Status == ProductStatusConstants.Deleted)
            throw new KeyNotFoundException($"Nội dung số '{productCode}' không tồn tại hoặc đã bị xóa");

        return product;
    }

    private async Task<ProductMaterialEntity> BuildMarketplaceProductMaterialAsync(Products product, int teacherId, CreateProductMaterialRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.MaterialCode))
            throw new InvalidOperationException("Vui lòng cung cấp MaterialCode khi thêm học liệu từ marketplace");

        var material = await _unitOfWork.TeacherRepository.GetApprovedMaterialByCodeAsync(request.MaterialCode)
            ?? throw new KeyNotFoundException($"Học liệu marketplace '{request.MaterialCode}' không tồn tại hoặc chưa được duyệt");

        var hasPurchasedMaterial = await _unitOfWork.TeacherRepository.HasTeacherPurchasedAsync(teacherId, material.MaterialId);
        if (!hasPurchasedMaterial)
            throw new InvalidOperationException("Bạn cần mua học liệu trước khi thêm vào sản phẩm");

        var alreadyExists = await _unitOfWork.ProductMaterialRepository
            .ExistsMarketplaceMaterialInProductAsync(product.ProductId, material.MaterialId);
        if (alreadyExists)
            throw new InvalidOperationException("Học liệu marketplace này đã tồn tại trong sản phẩm");

        return new ProductMaterialEntity
        {
            ProductId = product.ProductId,
            MaterialId = material.MaterialId,
            ProductMaterialCode = GenerateProductMaterialCode(product.ProductId),
            SourceType = MarketplaceSourceType,
            Title = material.Title,
            Type = material.Type,
            ResourceUrl = material.ResourceUrl,
            PreviewUrl = material.PreviewUrl,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static ProductMaterialEntity BuildUploadedProductMaterial(Products product, CreateProductMaterialRequestDto request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            throw new InvalidOperationException("Vui lòng cung cấp tiêu đề khi tải học liệu trực tiếp");

        if (string.IsNullOrWhiteSpace(request.Type))
            throw new InvalidOperationException("Vui lòng cung cấp loại học liệu khi tải trực tiếp");

        if (string.IsNullOrWhiteSpace(request.ResourceUrl))
            throw new InvalidOperationException("Vui lòng cung cấp ResourceUrl khi tải học liệu trực tiếp");

        return new ProductMaterialEntity
        {
            ProductId = product.ProductId,
            ProductMaterialCode = GenerateProductMaterialCode(product.ProductId),
            SourceType = UploadSourceType,
            Title = request.Title.Trim(),
            Type = request.Type.Trim(),
            ResourceUrl = request.ResourceUrl.Trim(),
            PreviewUrl = string.IsNullOrWhiteSpace(request.PreviewUrl) ? null : request.PreviewUrl.Trim(),
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string NormalizeSourceType(string sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            throw new InvalidOperationException("Vui lòng cung cấp sourceType");

        if (sourceType.Equals(MarketplaceSourceType, StringComparison.OrdinalIgnoreCase))
            return MarketplaceSourceType;

        if (sourceType.Equals(UploadSourceType, StringComparison.OrdinalIgnoreCase))
            return UploadSourceType;

        throw new InvalidOperationException("sourceType không hợp lệ. Chỉ chấp nhận 'Marketplace' hoặc 'Upload'");
    }

    private static string GenerateProductMaterialCode(int productId)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var randomSuffix = Random.Shared.Next(100, 999);
        return $"prm_{productId}_{timestamp}_{randomSuffix}";
    }

    private static ProductMaterialResponseDto MapToResponse(ProductMaterialEntity productMaterial, string productCode)
    {
        return new ProductMaterialResponseDto
        {
            ProductMaterialCode = productMaterial.ProductMaterialCode,
            ProductCode = productCode,
            SourceType = productMaterial.SourceType,
            MaterialCode = productMaterial.Material?.MaterialCode,
            Title = productMaterial.Title,
            Type = productMaterial.Type,
            ResourceUrl = productMaterial.ResourceUrl,
            PreviewUrl = productMaterial.PreviewUrl,
            CreatedAt = productMaterial.CreatedAt,
            UpdatedAt = productMaterial.UpdatedAt
        };
    }
}
