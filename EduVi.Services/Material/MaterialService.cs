using EduVi.Contracts.DTOs.Material;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace EduVi.Services.Material;

public class MaterialService : IMaterialService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaterialService> _logger;

    private const long MaxImageFileSizeBytes = 10 * 1024 * 1024; // 10MB
    private const long MaxVideoFileSizeBytes = 100 * 1024 * 1024; // 100MB

    private static readonly string[] AllowedFileTypes = ["image", "video"];

    private static readonly Dictionary<string, string[]> AllowedContentTypesByFileType = new()
    {
        ["image"] = ["image/jpeg", "image/png", "image/jpg", "image/webp", "image/svg+xml", "image/gif"],
        ["video"] = ["video/mp4", "video/webm", "video/quicktime"]
    };

    public MaterialService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<MaterialService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // EXPERT: quản lý materials
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<MaterialResponseDto> UploadFileMaterialAsync(int expertId, UploadFileMaterialRequestDto request)
    {
        var type = request.Type.ToLower();
        if (!AllowedFileTypes.Contains(type))
            throw new InvalidOperationException($"Type không hợp lệ cho file upload. Cho phép: {string.Join(", ", AllowedFileTypes)}");

        ValidateFileSizeByType(request.File, type);

        if (!AllowedContentTypesByFileType.TryGetValue(type, out var allowedContentTypes)
            || !allowedContentTypes.Contains(request.File.ContentType))
        {
            throw new InvalidOperationException(
                $"File không hợp lệ cho loại '{type}'. Cho phép: {string.Join(", ", AllowedContentTypesByFileType[type])}");
        }

        await ValidateExpertIsVerifiedAsync(expertId);
        var (subjectId, gradeId) = await ResolveSubjectGradeAsync(request.SubjectCode, request.GradeCode);

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");
        var storageClient = await StorageClient.CreateAsync();

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var materialCode = $"mat_{expertId}_{type}_{timestamp}";

        // Upload file chính
        var fileExtension = Path.GetExtension(request.File.FileName);
        var objectName = $"materials/{expertId}/{materialCode}{fileExtension}";
        using var stream = request.File.OpenReadStream();
        var uploadStart = Stopwatch.GetTimestamp();
        await storageClient.UploadObjectAsync(bucketName, objectName, request.File.ContentType, stream);
        var resourceUrl = $"gs://{bucketName}/{objectName}";
        _logger.LogInformation("Material file GCS upload completed in {ElapsedMs}ms for expertId {ExpertId}: {GcsPath}",
            Stopwatch.GetElapsedTime(uploadStart).TotalMilliseconds, expertId, resourceUrl);

        // Upload preview nếu có
        string? previewUrl = null;
        if (request.PreviewFile != null)
        {
            var previewExt = Path.GetExtension(request.PreviewFile.FileName);
            var previewObjectName = $"materials/{expertId}/{materialCode}_preview{previewExt}";
            using var previewStream = request.PreviewFile.OpenReadStream();
            await storageClient.UploadObjectAsync(bucketName, previewObjectName, request.PreviewFile.ContentType, previewStream);
            previewUrl = $"gs://{bucketName}/{previewObjectName}";
        }

        var material = BuildMaterial(materialCode, expertId, subjectId, gradeId, request.Title, request.Description, type, request.Price, resourceUrl, previewUrl);
        await _unitOfWork.ExpertRepository.CreateMaterialAsync(material);
        await _unitOfWork.SaveChangesAsync();
        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task<List<MaterialResponseDto>> GetMyMaterialsAsync(int expertId)
    {
        var materials = await _unitOfWork.ExpertRepository.GetMaterialsByExpertIdAsync(expertId);
        return materials.Select(m => MapToResponseDto(m, includeResourceUrl: true)).ToList();
    }

    public async Task<MaterialResponseDto> UpdateMaterialAsync(int expertId, string materialCode, UpdateMaterialRequestDto request)
    {
        var material = await _unitOfWork.ExpertRepository.GetMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại");

        if (material.ExpertId != expertId)
            throw new InvalidOperationException("Bạn không có quyền sửa material này");

        if (material.ApprovalStatus == 1)
            throw new InvalidOperationException("Không thể sửa material đã được duyệt");

        if (!string.IsNullOrWhiteSpace(request.Title))
            material.Title = request.Title;

        if (request.Description != null)
            material.Description = request.Description;

        if (request.Price.HasValue)
            material.Price = request.Price.Value;

        if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var subject = await _unitOfWork.ExpertRepository.GetSubjectByCodeAsync(request.SubjectCode)
                ?? throw new KeyNotFoundException($"SubjectCode '{request.SubjectCode}' không tồn tại");
            material.SubjectId = subject.SubjectId;
        }

        if (!string.IsNullOrWhiteSpace(request.GradeCode))
        {
            var grade = await _unitOfWork.ExpertRepository.GetGradeByCodeAsync(request.GradeCode)
                ?? throw new KeyNotFoundException($"GradeCode '{request.GradeCode}' không tồn tại");
            material.GradeId = grade.GradeId;
        }

        // Reset approval khi sửa → phải duyệt lại
        material.ApprovalStatus = 0;
        material.ApproverId = null;

        _unitOfWork.ExpertRepository.UpdateMaterial(material);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task DeleteMaterialAsync(int expertId, string materialCode)
    {
        var material = await _unitOfWork.ExpertRepository.GetMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại");

        if (material.ExpertId != expertId)
            throw new InvalidOperationException("Bạn không có quyền xóa material này");

        if (material.ApprovalStatus == 1)
            throw new InvalidOperationException("Không thể xóa material đã được duyệt. Liên hệ Staff để hỗ trợ");

        _unitOfWork.ExpertRepository.DeleteMaterial(material);
        await _unitOfWork.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // STAFF: kiểm duyệt materials
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<List<MaterialResponseDto>> GetPendingMaterialsAsync()
    {
        var materials = await _unitOfWork.StaffRepository.GetPendingMaterialsAsync();
        return materials.Select(m => MapToResponseDto(m, includeResourceUrl: true)).ToList();
    }

    public async Task<MaterialResponseDto> GetMaterialDetailForStaffAsync(string materialCode)
    {
        var material = await _unitOfWork.StaffRepository.GetMaterialByCodeWithDetailsAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại");

        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task ReviewMaterialAsync(int staffId, string materialCode, ReviewMaterialRequestDto request)
    {
        var material = await _unitOfWork.StaffRepository.GetMaterialByCodeWithDetailsAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại");

        if (material.ApprovalStatus != 0)
            throw new InvalidOperationException("Material này đã được xử lý trước đó");

        if (request.Approved)
        {
            material.ApprovalStatus = 1; // Approved
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new InvalidOperationException("Phải cung cấp lý do khi từ chối material");

            material.ApprovalStatus = 2; // Rejected
        }

        material.ApproverId = staffId;

        _unitOfWork.StaffRepository.UpdateMaterial(material);
        await _unitOfWork.SaveChangesAsync();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // TEACHER: browse và mua materials
    // ══════════════════════════════════════════════════════════════════════════

    public async Task<List<MaterialResponseDto>> BrowseMaterialsAsync(
        string? subjectCode, string? gradeCode, string? type, string? keyword)
    {
        var materials = await _unitOfWork.TeacherRepository.GetApprovedMaterialsAsync(subjectCode, gradeCode, type, keyword);
        // Teacher browse → không thấy ResourceUrl (chưa mua)
        return materials.Select(m => MapToResponseDto(m, includeResourceUrl: false)).ToList();
    }

    public async Task<MaterialResponseDto> GetMaterialDetailForTeacherAsync(int teacherId, string materialCode)
    {
        var material = await _unitOfWork.TeacherRepository.GetApprovedMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại hoặc chưa được duyệt");

        // Kiểm tra đã mua chưa → nếu mua rồi thì show ResourceUrl
        var hasPurchased = await _unitOfWork.TeacherRepository.HasTeacherPurchasedAsync(teacherId, material.MaterialId);

        return MapToResponseDto(material, includeResourceUrl: hasPurchased);
    }

    public async Task<PurchasedMaterialResponseDto> PurchaseMaterialAsync(int teacherId, string materialCode)
    {
        var material = await _unitOfWork.TeacherRepository.GetApprovedMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Material '{materialCode}' không tồn tại hoặc chưa được duyệt");

        // Kiểm tra đã mua chưa
        var alreadyPurchased = await _unitOfWork.TeacherRepository.HasTeacherPurchasedAsync(teacherId, material.MaterialId);
        if (alreadyPurchased)
            throw new InvalidOperationException("Bạn đã mua material này rồi");

        var price = material.Price ?? 0;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Nếu material có giá > 0, trừ tiền ví
            if (price > 0)
            {
                var wallet = await _unitOfWork.TeacherRepository.GetWalletByUserIdAsync(teacherId)
                    ?? throw new InvalidOperationException("Bạn chưa có ví. Vui lòng nạp tiền trước");

                if ((wallet.Balance ?? 0) < price)
                    throw new InvalidOperationException($"Số dư ví không đủ. Cần {price:N0} VND, hiện có {wallet.Balance:N0} VND");

                var balanceBefore = wallet.Balance ?? 0;
                wallet.Balance = balanceBefore - price;
                wallet.LastUpdated = DateTime.UtcNow;
                _unitOfWork.TeacherRepository.UpdateWallet(wallet);

                // Tạo transaction record
                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                var transaction = new WalletTransactions
                {
                    WalletId = wallet.WalletId,
                    OrderCode = orderCode,
                    TransactionType = "BUY_MATERIAL",
                    Amount = -price,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = wallet.Balance,
                    Status = 1, // COMPLETED
                    Description = $"Mua material: {material.Title} ({materialCode})",
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.TeacherRepository.CreateWalletTransactionAsync(transaction);
            }

            // Tạo bản ghi TeacherMaterials
            var teacherMaterial = new TeacherMaterials
            {
                TeacherId = teacherId,
                MaterialId = material.MaterialId,
                PurchasedDate = DateTime.UtcNow
            };
            await _unitOfWork.TeacherRepository.CreateTeacherMaterialAsync(teacherMaterial);

            await _unitOfWork.CommitTransactionAsync();

            return MapToPurchasedDto(material, teacherMaterial);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<List<PurchasedMaterialResponseDto>> GetPurchasedMaterialsAsync(int teacherId)
    {
        var teacherMaterials = await _unitOfWork.TeacherRepository.GetPurchasedMaterialsAsync(teacherId);
        return teacherMaterials.Select(tm => MapToPurchasedDto(tm.Material, tm)).ToList();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private async Task ValidateExpertIsVerifiedAsync(int expertId)
    {
        var expert = await _unitOfWork.ExpertRepository.GetExpertByIdAsync(expertId)
            ?? throw new KeyNotFoundException($"Expert {expertId} không tồn tại");

        if (expert.IsVerified != true)
            throw new InvalidOperationException("Expert chưa được xác thực. Vui lòng hoàn tất xác thực trước khi upload material");
    }

    private async Task<(int? subjectId, int? gradeId)> ResolveSubjectGradeAsync(string? subjectCode, string? gradeCode)
    {
        int? subjectId = null;
        int? gradeId = null;

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            var subject = await _unitOfWork.ExpertRepository.GetSubjectByCodeAsync(subjectCode)
                ?? throw new KeyNotFoundException($"SubjectCode '{subjectCode}' không tồn tại");
            subjectId = subject.SubjectId;
        }

        if (!string.IsNullOrWhiteSpace(gradeCode))
        {
            var grade = await _unitOfWork.ExpertRepository.GetGradeByCodeAsync(gradeCode)
                ?? throw new KeyNotFoundException($"GradeCode '{gradeCode}' không tồn tại");
            gradeId = grade.GradeId;
        }

        return (subjectId, gradeId);
    }

    private static Materials BuildMaterial(
        string materialCode, int expertId, int? subjectId, int? gradeId,
        string title, string? description, string type, decimal? price,
        string resourceUrl, string? previewUrl)
    {
        return new Materials
        {
            MaterialCode = materialCode,
            ExpertId = expertId,
            SubjectId = subjectId,
            GradeId = gradeId,
            Title = title,
            Description = description,
            Type = type,
            Price = price ?? 0,
            ResourceUrl = resourceUrl,
            PreviewUrl = previewUrl,
            ApprovalStatus = 0, // Pending
            CreatedAt = DateTime.UtcNow
        };
    }

    private static void ValidateFileSizeByType(Microsoft.AspNetCore.Http.IFormFile file, string type)
    {
        if (file.Length <= 0)
            throw new InvalidOperationException("File upload rỗng hoặc không hợp lệ");

        var maxFileSizeBytes = type == "video" ? MaxVideoFileSizeBytes : MaxImageFileSizeBytes;
        if (file.Length > maxFileSizeBytes)
        {
            var maxFileSizeMb = type == "video" ? 100 : 10;
            throw new InvalidOperationException($"File {type} vượt quá giới hạn {maxFileSizeMb}MB");
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // MAPPING HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static MaterialResponseDto MapToResponseDto(Materials material, bool includeResourceUrl)
    {
        return new MaterialResponseDto
        {
            MaterialCode = material.MaterialCode,
            Title = material.Title,
            Description = material.Description,
            Type = material.Type,
            Price = material.Price,
            PreviewUrl = material.PreviewUrl,
            ResourceUrl = includeResourceUrl ? material.ResourceUrl : null,
            SubjectCode = material.Subject?.SubjectCode,
            SubjectName = material.Subject?.SubjectName,
            GradeCode = material.Grade?.GradeCode,
            GradeName = material.Grade?.GradeName,
            ApprovalStatus = material.ApprovalStatus ?? 0,
            ExpertCode = material.Expert?.ExpertCode,
            ExpertName = material.Expert?.Expert?.FullName, // Expert → Users.FullName
            CreatedAt = material.CreatedAt
        };
    }

    private static PurchasedMaterialResponseDto MapToPurchasedDto(Materials material, TeacherMaterials teacherMaterial)
    {
        return new PurchasedMaterialResponseDto
        {
            MaterialCode = material.MaterialCode,
            Title = material.Title,
            Description = material.Description,
            Type = material.Type,
            Price = material.Price,
            ResourceUrl = material.ResourceUrl,
            PreviewUrl = material.PreviewUrl,
            SubjectCode = material.Subject?.SubjectCode,
            SubjectName = material.Subject?.SubjectName,
            GradeCode = material.Grade?.GradeCode,
            GradeName = material.Grade?.GradeName,
            ExpertCode = material.Expert?.ExpertCode,
            ExpertName = material.Expert?.Expert?.FullName,
            PurchasedDate = teacherMaterial.PurchasedDate ?? DateTime.MinValue
        };
    }
}
