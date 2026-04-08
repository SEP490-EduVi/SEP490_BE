using EduVi.Contracts.DTOs.Expert;
using EduVi.Contracts.DTOs.Profile;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

namespace EduVi.Services.Expert;

public class ExpertService : IExpertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ExpertService> _logger;

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/jpg", "application/pdf"];

    private static readonly string[] AllowedFileTypes =
        ["degree", "certificate", "id_card", "other"];

    public ExpertService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IConnectionMultiplexer redis,
        ILogger<ExpertService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _redis = redis;
        _logger = logger;
    }

    // ── Expert: nộp hồ sơ ─────────────────────────────────────────────────────

    public async Task<ExpertVerificationDto> UploadVerificationAsync(int expertId, UploadVerificationRequestDto request)
    {
        if (!AllowedContentTypes.Contains(request.File.ContentType))
            throw new InvalidOperationException("Chỉ chấp nhận file ảnh (JPG, PNG) hoặc PDF cho hồ sơ xác thực");

        if (!AllowedFileTypes.Contains(request.FileType.ToLower()))
            throw new InvalidOperationException($"FileType không hợp lệ. Hợp lệ: {string.Join(", ", AllowedFileTypes)}");

        var expert = await _unitOfWork.ExpertRepository.GetExpertByIdAsync(expertId)
            ?? throw new KeyNotFoundException($"Expert {expertId} không tồn tại");

        if (expert.IsVerified == true)
            throw new InvalidOperationException("Tài khoản đã được xác thực. Không cần nộp thêm hồ sơ");

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");

        var storageClient = await StorageClient.CreateAsync();

        var fileExtension = Path.GetExtension(request.File.FileName);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
        var verificationCode = $"vrf_{expertId}_{request.FileType.ToLower()}_{timestamp}";
        var objectName = $"expert_verifications/{expertId}/{verificationCode}{fileExtension}";

        using var stream = request.File.OpenReadStream();
        var uploadStart = Stopwatch.GetTimestamp();
        await storageClient.UploadObjectAsync(bucketName, objectName, request.File.ContentType, stream);
        var uploadElapsed = Stopwatch.GetElapsedTime(uploadStart);

        var gcsPath = $"gs://{bucketName}/{objectName}";
        _logger.LogInformation("Expert verification GCS upload completed in {ElapsedMs}ms for expertId {ExpertId}: {GcsPath}",
            uploadElapsed.TotalMilliseconds, expertId, gcsPath);

        var verification = new ExpertVerifications
        {
            VerificationCode = verificationCode,
            ExpertId = expertId,
            FileUrl = gcsPath,
            FileType = request.FileType.ToLower(),
            Description = request.Description,
            Status = VerificationStatus.Pending,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.ExpertRepository.CreateVerificationAsync(verification);
        await _unitOfWork.SaveChangesAsync();

        return MapToExpertDto(verification);
    }

    public async Task<List<ExpertVerificationDto>> GetMyVerificationsAsync(int expertId)
    {
        var verifications = await _unitOfWork.ExpertRepository.GetVerificationsByExpertAsync(expertId);
        return verifications.Select(MapToExpertDto).ToList();
    }

    public async Task DeleteVerificationAsync(int expertId, string verificationCode)
    {
        var verification = await _unitOfWork.ExpertRepository.GetVerificationByCodeAsync(verificationCode)
            ?? throw new KeyNotFoundException($"Verification '{verificationCode}' không tồn tại");

        if (verification.ExpertId != expertId)
            throw new InvalidOperationException("Hồ sơ không thuộc về bạn");

        if (verification.Status == VerificationStatus.Approved)
            throw new InvalidOperationException("Không thể xóa hồ sơ đã được duyệt");

        var bucketName = _configuration["GCS:BucketName"]
            ?? throw new InvalidOperationException("GCS BucketName not configured");

        var storageClient = await StorageClient.CreateAsync();
        var objectName = verification.FileUrl.Replace($"gs://{bucketName}/", "");

        try
        {
            var deleteStart = Stopwatch.GetTimestamp();
            await storageClient.DeleteObjectAsync(bucketName, objectName);
            var deleteElapsed = Stopwatch.GetElapsedTime(deleteStart);
            _logger.LogInformation("Expert verification GCS delete completed in {ElapsedMs}ms: {FilePath}",
                deleteElapsed.TotalMilliseconds, verification.FileUrl);
        }
        catch (Google.GoogleApiException ex) when (ex.HttpStatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Expert verification GCS file not found during delete, skipping: {FilePath}", verification.FileUrl);
        }

        _unitOfWork.ExpertRepository.DeleteVerification(verification);
        await _unitOfWork.SaveChangesAsync();
    }

    // ── Staff: kiểm duyệt ─────────────────────────────────────────────────────

    public async Task<List<ExpertVerificationStaffDto>> GetPendingVerificationsAsync()
    {
        var verifications = await _unitOfWork.StaffRepository.GetPendingVerificationsAsync();
        return verifications
            .Select(verification =>
            {
                var proxyUrl = $"/api/staff/verifications/{verification.VerificationCode}/file";
                return MapToStaffDto(verification, proxyUrl);
            })
            .ToList();
    }

    public async Task<ExpertVerificationFileDto> GetVerificationFileAsync(string verificationCode)
    {
        var verification = await _unitOfWork.StaffRepository.GetVerificationByCodeAsync(verificationCode)
            ?? throw new KeyNotFoundException($"Verification '{verificationCode}' không tồn tại");

        var (bucketName, objectName) = ParseGcsPath(verification.FileUrl);
        var storageClient = await StorageClient.CreateAsync();
        var googleStorageObject = await storageClient.GetObjectAsync(bucketName, objectName);

        await using var memoryStream = new MemoryStream();
        await storageClient.DownloadObjectAsync(googleStorageObject, memoryStream);

        return new ExpertVerificationFileDto
        {
            FileBytes = memoryStream.ToArray(),
            ContentType = string.IsNullOrWhiteSpace(googleStorageObject.ContentType)
                ? "application/octet-stream"
                : googleStorageObject.ContentType,
            FileName = Path.GetFileName(objectName)
        };
    }

    public async Task ReviewVerificationAsync(int staffId, string verificationCode, ReviewVerificationRequestDto request)
    {
        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new InvalidOperationException("Phải cung cấp lý do từ chối khi Reject hồ sơ");

        var verification = await _unitOfWork.StaffRepository.GetVerificationByCodeAsync(verificationCode)
            ?? throw new KeyNotFoundException($"Verification '{verificationCode}' không tồn tại");

        if (verification.Status != VerificationStatus.Pending)
            throw new InvalidOperationException($"Hồ sơ này đã được xử lý (trạng thái: {VerificationStatus.GetStatusName(verification.Status)})");

        verification.Status = request.Approved ? VerificationStatus.Approved : VerificationStatus.Rejected;
        verification.RejectionReason = request.Approved ? null : request.RejectionReason;
        verification.ReviewedAt = DateTime.UtcNow;
        verification.ReviewedByStaffId = staffId;
        _unitOfWork.StaffRepository.UpdateVerification(verification);

        // Dùng navigation property đã được Include sẵn — tránh load thêm instance thứ 2 gây EF tracking conflict
        var expert = verification.Expert
            ?? throw new InvalidOperationException($"Expert {verification.ExpertId} không tồn tại");

        if (request.Approved)
        {
            expert.IsVerified = true;
            _unitOfWork.StaffRepository.UpdateExpert(expert);

            // Force Expert re-login để JWT mới có expert_is_verified = true
            var redisDb = _redis.GetDatabase();
            await redisDb.KeyDeleteAsync($"token:{expert.ExpertId}");
            _logger.LogInformation("Expert {ExpertId} verification approved by Staff {StaffId}. Forced re-login.", expert.ExpertId, staffId);
        }
        else
        {
            // Chỉ set IsVerified = false nếu Expert chưa có hồ sơ approved nào khác
            var hasOtherApproved = await _unitOfWork.StaffRepository
                .HasOtherApprovedVerificationAsync(expert.ExpertId, verificationCode);

            if (!hasOtherApproved)
            {
                expert.IsVerified = false;
                _unitOfWork.StaffRepository.UpdateExpert(expert);
            }

            _logger.LogInformation("Expert {ExpertId} verification rejected by Staff {StaffId}. Reason: {Reason}",
                expert.ExpertId, staffId, request.RejectionReason);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (string BucketName, string ObjectName) ParseGcsPath(string gcsPath)
    {
        if (string.IsNullOrWhiteSpace(gcsPath) || !gcsPath.StartsWith("gs://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Đường dẫn file GCS không hợp lệ");

        var pathWithoutScheme = gcsPath["gs://".Length..];
        var separatorIndex = pathWithoutScheme.IndexOf('/');

        if (separatorIndex <= 0 || separatorIndex == pathWithoutScheme.Length - 1)
            throw new InvalidOperationException("Đường dẫn file GCS không hợp lệ");

        var bucketName = pathWithoutScheme[..separatorIndex];
        var objectName = pathWithoutScheme[(separatorIndex + 1)..];
        return (bucketName, objectName);
    }

    private static ExpertVerificationDto MapToExpertDto(ExpertVerifications verification)
    {
        return new ExpertVerificationDto
        {
            VerificationCode = verification.VerificationCode,
            FileType = verification.FileType,
            Description = verification.Description,
            Status = verification.Status,
            RejectionReason = verification.RejectionReason,
            UploadedAt = verification.UploadedAt,
            ReviewedAt = verification.ReviewedAt
        };
    }

    private static ExpertVerificationStaffDto MapToStaffDto(ExpertVerifications verification, string fileUrl)
    {
        return new ExpertVerificationStaffDto
        {
            VerificationCode = verification.VerificationCode,
            ExpertId = verification.ExpertId,
            ExpertName = verification.Expert?.Expert?.FullName ?? "Unknown",
            ExpertEmail = verification.Expert?.Expert?.Email ?? "Unknown",
            FileType = verification.FileType,
            Description = verification.Description,
            Status = verification.Status,
            RejectionReason = verification.RejectionReason,
            UploadedAt = verification.UploadedAt,
            ReviewedAt = verification.ReviewedAt,
            FileUrl = fileUrl
        };
    }

    // ── Profile ───────────────────────────────────────────────────────────────

    public async Task<ExpertProfileResponse> GetProfileAsync(int userId)
    {
        var expert = await _unitOfWork.ExpertRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin chuyên gia.");

        return new ExpertProfileResponse
        {
            UserCode    = expert.Expert.UserCode,
            FullName    = expert.Expert.FullName,
            Email       = expert.Expert.Email,
            PhoneNumber = expert.Expert.PhoneNumber,
            AvatarUrl   = expert.Expert.AvatarUrl,
            Bio         = expert.Bio,
            IsVerified  = expert.IsVerified,
        };
    }

    public async Task UpdateProfileAsync(int userId, UpdateExpertProfileRequest request)
    {
        var expert = await _unitOfWork.ExpertRepository.GetProfileByUserIdAsync(userId)
            ?? throw new KeyNotFoundException("Không tìm thấy thông tin chuyên gia.");

        if (request.FullName is not null)
            expert.Expert.FullName = request.FullName;

        if (request.PhoneNumber is not null)
            expert.Expert.PhoneNumber = request.PhoneNumber;

        if (request.Bio is not null)
            expert.Bio = request.Bio;

        await _unitOfWork.SaveChangesAsync();
    }
}

/// <summary>Hằng số trạng thái ExpertVerification</summary>
public static class VerificationStatus
{
    public const int Pending = 0;
    public const int Approved = 1;
    public const int Rejected = 2;

    public static string GetStatusName(int status) => status switch
    {
        Pending => "PENDING",
        Approved => "APPROVED",
        Rejected => "REJECTED",
        _ => "UNKNOWN"
    };
}
