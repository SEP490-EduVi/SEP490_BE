using EduVi.Contracts.DTOs.Material;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Material;

public class MaterialService : IMaterialService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MaterialService> _logger;

    private const ulong MaxImageFileSizeBytes = 10UL * 1024 * 1024; // 10MB
    private const ulong MaxVideoFileSizeBytes = 100UL * 1024 * 1024; // 100MB

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
        var type = request.Type.ToLowerInvariant();
        if (!AllowedFileTypes.Contains(type))
            throw new InvalidOperationException($"Loại tệp tải lên không hợp lệ. Chấp nhận: {string.Join(", ", AllowedFileTypes)}");

        await ValidateExpertIsVerifiedAsync(expertId);
        await ValidateExpertPendingLimitAsync(expertId);
        var (subjectId, gradeId) = await ResolveSubjectGradeAsync(request.SubjectCode, request.GradeCode);

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("Chưa cấu hình tên bucket GCS");
        var storageClient = await StorageClient.CreateAsync();

        var resourceObjectName = ParseAndValidateObjectName(request.ResourceUrl, bucketName, expertId, "tài nguyên");
        var resourceObject = await GetObjectOrThrowAsync(storageClient, bucketName, resourceObjectName, "tài nguyên");
        ValidateFileSizeByType(resourceObject.Size, type);
        ValidateContentTypeByType(resourceObject.ContentType, type);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var materialCode = $"mat_{expertId}_{type}_{timestamp}";

        var resourceUrl = $"gs://{bucketName}/{resourceObjectName}";

        // Validate preview object nếu có
        string? previewUrl = null;
        if (!string.IsNullOrWhiteSpace(request.PreviewUrl))
        {
            var previewObjectName = ParseAndValidateObjectName(request.PreviewUrl, bucketName, expertId, "xem trước");
            var previewObject = await GetObjectOrThrowAsync(storageClient, bucketName, previewObjectName, "xem trước");

            if (string.IsNullOrWhiteSpace(previewObject.ContentType)
                || !previewObject.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Tệp xem trước phải là ảnh hợp lệ");
            }

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
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại");

        if (material.ExpertId != expertId)
            throw new InvalidOperationException("Bạn không có quyền sửa học liệu này");

        if (material.ApprovalStatus == 1)
            throw new InvalidOperationException("Không thể sửa học liệu đã được duyệt");

        if (!string.IsNullOrWhiteSpace(request.Title))
            material.Title = request.Title;

        if (request.Description != null)
            material.Description = request.Description;

        if (request.Price.HasValue)
            material.Price = request.Price.Value;

        if (!string.IsNullOrWhiteSpace(request.SubjectCode))
        {
            var subject = await _unitOfWork.ExpertRepository.GetSubjectByCodeAsync(request.SubjectCode)
                ?? throw new KeyNotFoundException($"Mã môn học '{request.SubjectCode}' không tồn tại");
            material.SubjectId = subject.SubjectId;
        }

        if (!string.IsNullOrWhiteSpace(request.GradeCode))
        {
            var grade = await _unitOfWork.ExpertRepository.GetGradeByCodeAsync(request.GradeCode)
                ?? throw new KeyNotFoundException($"Mã khối lớp '{request.GradeCode}' không tồn tại");
            material.GradeId = grade.GradeId;
        }

        // Reset approval khi sửa → phải duyệt lại
        material.ApprovalStatus = 0;
        material.RejectionReason = null;
        material.ApproverId = null;

        _unitOfWork.ExpertRepository.UpdateMaterial(material);
        await _unitOfWork.SaveChangesAsync();

        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task DeleteMaterialAsync(int expertId, string materialCode)
    {
        var material = await _unitOfWork.ExpertRepository.GetMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại");

        if (material.ExpertId != expertId)
            throw new InvalidOperationException("Bạn không có quyền xóa học liệu này");

        if (material.ApprovalStatus == 1)
            throw new InvalidOperationException("Không thể xóa học liệu đã được duyệt. Vui lòng liên hệ nhân viên để được hỗ trợ");

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
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại");

        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task ReviewMaterialAsync(int staffId, string materialCode, ReviewMaterialRequestDto request)
    {
        var material = await _unitOfWork.StaffRepository.GetMaterialByCodeWithDetailsAsync(materialCode)
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại");

        if (material.ApprovalStatus != 0)
            throw new InvalidOperationException("Học liệu này đã được xử lý trước đó");

        if (request.Approved)
        {
            material.ApprovalStatus = 1; // Approved
            material.RejectionReason = null;
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.RejectionReason))
                throw new InvalidOperationException("Phải cung cấp lý do khi từ chối học liệu");

            material.ApprovalStatus = 2; // Rejected
            material.RejectionReason = request.RejectionReason;
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
        // Teacher browse → được xem đầy đủ nội dung (bao gồm video/hình) để tham khảo trước khi mua.
        return materials.Select(m => MapToResponseDto(m, includeResourceUrl: true)).ToList();
    }

    public async Task<MaterialResponseDto> GetMaterialDetailForTeacherAsync(int teacherId, string materialCode)
    {
        var material = await _unitOfWork.TeacherRepository.GetApprovedMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại hoặc chưa được duyệt");

        return MapToResponseDto(material, includeResourceUrl: true);
    }

    public async Task<PurchasedMaterialResponseDto> PurchaseMaterialAsync(int teacherId, string materialCode)
    {
        var material = await _unitOfWork.TeacherRepository.GetApprovedMaterialByCodeAsync(materialCode)
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại hoặc chưa được duyệt");

        // Kiểm tra đã mua chưa
        var alreadyPurchased = await _unitOfWork.TeacherRepository.HasTeacherPurchasedAsync(teacherId, material.MaterialId);
        if (alreadyPurchased)
            throw new InvalidOperationException("Bạn đã mua học liệu này rồi");

        var price = material.Price ?? 0;
        var baseOrderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100 + Random.Shared.Next(10, 99);

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
                // +0/+1/+2 để 3 rows cùng batch đều unique, vẫn traceable theo cùng base
                var transaction = new WalletTransactions
                {
                    WalletId = wallet.WalletId,
                    OrderCode = baseOrderCode,
                    TransactionType = "BUY_MATERIAL",
                    Amount = -price,
                    BalanceBefore = balanceBefore,
                    BalanceAfter = wallet.Balance,
                    Status = 1, // COMPLETED
                    Description = $"Mua học liệu: {material.Title}",
                    MaterialId = material.MaterialId,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.TeacherRepository.CreateWalletTransactionAsync(transaction);

                var adminWallet = await _unitOfWork.AdminRepository.GetAdminWalletAsync()
                    ?? throw new InvalidOperationException("Không tìm thấy ví nền tảng của quản trị viên. Vui lòng liên hệ hỗ trợ.");

                var adminRevenue = price;
                var adminTransactionType = "MATERIAL_ADMIN_REVENUE";
                var adminTransactionDescription = $"Doanh thu học liệu quản trị (100%): {material.Title}";

                if (material.ExpertId.HasValue)
                {
                    // Material thuộc expert: chia doanh thu 70/30 (expert/platform).
                    var expertRevenue = Math.Round(price * 0.7m, 0);
                    var platformFee = price - expertRevenue;

                    // ExpertId == UserId trong model (FK đã map 1-1)
                    var expertWallet = await _unitOfWork.ExpertRepository.GetWalletByUserIdAsync(material.ExpertId.Value)
                        ?? throw new InvalidOperationException("Chuyên gia chưa có ví. Vui lòng liên hệ hỗ trợ.");

                    var expertBalanceBefore = expertWallet.Balance ?? 0;
                    expertWallet.Balance = expertBalanceBefore + expertRevenue;
                    expertWallet.LastUpdated = DateTime.UtcNow;
                    _unitOfWork.ExpertRepository.UpdateWallet(expertWallet);

                    var expertTransaction = new WalletTransactions
                    {
                        WalletId = expertWallet.WalletId,
                        OrderCode = baseOrderCode + 1,
                        TransactionType = "MATERIAL_REVENUE",
                        Amount = expertRevenue,
                        BalanceBefore = expertBalanceBefore,
                        BalanceAfter = expertWallet.Balance,
                        Status = 1,
                        Description = $"Doanh thu học liệu (70%): {material.Title}",
                        MaterialId = material.MaterialId,
                        CreatedAt = DateTime.UtcNow
                    };
                    await _unitOfWork.ExpertRepository.CreateWalletTransactionAsync(expertTransaction);

                    adminRevenue = platformFee;
                    adminTransactionType = "MATERIAL_PLATFORM_FEE";
                    adminTransactionDescription = $"Phí nền tảng học liệu (30%): {material.Title}";
                }

                var adminBalanceBefore = adminWallet.Balance ?? 0;
                adminWallet.Balance = adminBalanceBefore + adminRevenue;
                adminWallet.LastUpdated = DateTime.UtcNow;
                _unitOfWork.AdminRepository.UpdateWallet(adminWallet);

                var adminTransaction = new WalletTransactions
                {
                    WalletId = adminWallet.WalletId,
                    OrderCode = baseOrderCode + 2,
                    TransactionType = adminTransactionType,
                    Amount = adminRevenue,
                    BalanceBefore = adminBalanceBefore,
                    BalanceAfter = adminWallet.Balance,
                    Status = 1,
                    Description = adminTransactionDescription,
                    MaterialId = material.MaterialId,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.AdminRepository.CreateWalletTransactionAsync(adminTransaction);
            }
            else
            {
                // Material miễn phí vẫn ghi nhận transaction amount = 0 để audit lịch sử nhận học liệu.
                var teacherWallet = await _unitOfWork.TeacherRepository.GetWalletByUserIdAsync(teacherId);
                var walletBalance = teacherWallet?.Balance ?? 0;

                var freeMaterialTransaction = new WalletTransactions
                {
                    WalletId = teacherWallet?.WalletId,
                    OrderCode = baseOrderCode,
                    TransactionType = "CLAIM_FREE_MATERIAL",
                    Amount = 0,
                    BalanceBefore = walletBalance,
                    BalanceAfter = walletBalance,
                    Status = 1,
                    Description = $"Nhận học liệu miễn phí: {material.Title}",
                    MaterialId = material.MaterialId,
                    CreatedAt = DateTime.UtcNow
                };
                await _unitOfWork.TeacherRepository.CreateWalletTransactionAsync(freeMaterialTransaction);
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

    public async Task<PurchasedMaterialResponseDto> GetPurchasedMaterialDetailAsync(int teacherId, string materialCode)
    {
        var purchasedMaterial = await _unitOfWork.TeacherRepository
            .GetPurchasedMaterialByCodeAsync(teacherId, materialCode)
            ?? throw new KeyNotFoundException($"Học liệu '{materialCode}' không tồn tại trong danh sách đã mua");

        return MapToPurchasedDto(purchasedMaterial.Material, purchasedMaterial);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private async Task ValidateExpertIsVerifiedAsync(int expertId)
    {
        var expert = await _unitOfWork.ExpertRepository.GetExpertByIdAsync(expertId)
            ?? throw new KeyNotFoundException($"Chuyên gia {expertId} không tồn tại");

        if (expert.IsVerified != true)
            throw new InvalidOperationException("Chuyên gia chưa được xác thực. Vui lòng hoàn tất xác thực trước khi tải lên học liệu");
    }

    private async Task ValidateExpertPendingLimitAsync(int expertId)
    {
        const int MaxPendingMaterials = 3;
        var pendingCount = await _unitOfWork.ExpertRepository.CountPendingMaterialsAsync(expertId);
        if (pendingCount >= MaxPendingMaterials)
            throw new InvalidOperationException($"Bạn đang có {pendingCount} học liệu chờ duyệt. Tối đa {MaxPendingMaterials} học liệu chờ duyệt cùng lúc. Vui lòng chờ nhân viên duyệt trước khi tải lên thêm.");
    }

    private async Task<(int? subjectId, int? gradeId)> ResolveSubjectGradeAsync(string? subjectCode, string? gradeCode)
    {
        int? subjectId = null;
        int? gradeId = null;

        if (!string.IsNullOrWhiteSpace(subjectCode))
        {
            var subject = await _unitOfWork.ExpertRepository.GetSubjectByCodeAsync(subjectCode)
                ?? throw new KeyNotFoundException($"Mã môn học '{subjectCode}' không tồn tại");
            subjectId = subject.SubjectId;
        }

        if (!string.IsNullOrWhiteSpace(gradeCode))
        {
            var grade = await _unitOfWork.ExpertRepository.GetGradeByCodeAsync(gradeCode)
                ?? throw new KeyNotFoundException($"Mã khối lớp '{gradeCode}' không tồn tại");
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

    private static void ValidateFileSizeByType(ulong? fileSizeBytes, string type)
    {
        if (!fileSizeBytes.HasValue || fileSizeBytes.Value <= 0)
            throw new InvalidOperationException("Tệp tải lên rỗng hoặc không hợp lệ");

        var maxFileSizeBytes = type == "video" ? MaxVideoFileSizeBytes : MaxImageFileSizeBytes;
        if (fileSizeBytes.Value > maxFileSizeBytes)
        {
            var maxFileSizeMb = type == "video" ? 100 : 10;
            throw new InvalidOperationException($"Tệp {type} vượt quá giới hạn {maxFileSizeMb}MB");
        }
    }

    private static void ValidateContentTypeByType(string? contentType, string type)
    {
        if (string.IsNullOrWhiteSpace(contentType))
            throw new InvalidOperationException("Không xác định được loại nội dung của tệp trên GCS");

        if (!AllowedContentTypesByFileType.TryGetValue(type, out var allowedContentTypes)
            || !allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Tệp không hợp lệ cho loại '{type}'. Cho phép: {string.Join(", ", AllowedContentTypesByFileType[type])}");
        }
    }

    private static string ParseAndValidateObjectName(string resourceUrl, string expectedBucketName, int expertId, string fieldName)
    {
        if (!Uri.TryCreate(resourceUrl, UriKind.Absolute, out var uri))
            throw new InvalidOperationException($"Đường dẫn {fieldName} không hợp lệ");

        string bucketName;
        string objectName;

        if (uri.Scheme.Equals("gs", StringComparison.OrdinalIgnoreCase))
        {
            bucketName = uri.Host;
            objectName = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        }
        else if (uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)
                 || uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase))
        {
            if (uri.Host.Equals("storage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                var pathParts = uri.AbsolutePath.TrimStart('/').Split('/', 2, StringSplitOptions.RemoveEmptyEntries);
                if (pathParts.Length < 2)
                    throw new InvalidOperationException($"Đường dẫn {fieldName} không hợp lệ");

                bucketName = pathParts[0];
                objectName = Uri.UnescapeDataString(pathParts[1]);
            }
            else if (uri.Host.EndsWith(".storage.googleapis.com", StringComparison.OrdinalIgnoreCase))
            {
                bucketName = uri.Host[..^".storage.googleapis.com".Length];
                objectName = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
            }
            else
            {
                throw new InvalidOperationException($"Đường dẫn {fieldName} phải thuộc dịch vụ lưu trữ Google Cloud");
            }
        }
        else
        {
            throw new InvalidOperationException($"Đường dẫn {fieldName} phải là gs:// hoặc https://storage.googleapis.com");
        }

        if (!bucketName.Equals(expectedBucketName, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Đường dẫn {fieldName} không thuộc bucket đã cấu hình");

        if (string.IsNullOrWhiteSpace(objectName))
            throw new InvalidOperationException($"Đường dẫn {fieldName} không chứa tên tệp hợp lệ");

        var expectedPrefix = $"materials/{expertId}/";
        if (!objectName.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Đường dẫn {fieldName} phải thuộc thư mục {expectedPrefix}");

        return objectName;
    }

    private static async Task<Google.Apis.Storage.v1.Data.Object> GetObjectOrThrowAsync(
        StorageClient storageClient,
        string bucketName,
        string objectName,
        string fieldName)
    {
        try
        {
            return await storageClient.GetObjectAsync(bucketName, objectName);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new KeyNotFoundException($"Không tìm thấy tệp {fieldName} trên GCS");
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
            RejectionReason = material.RejectionReason,
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
