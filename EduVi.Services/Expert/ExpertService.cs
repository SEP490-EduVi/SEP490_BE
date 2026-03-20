using EduVi.Contracts.DTOs.Expert;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Google.Apis.Auth.OAuth2;
using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using System.Diagnostics;

namespace EduVi.Services.Expert;

public class ExpertService : IExpertService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<ExpertService> _logger;

    private static readonly string[] AllowedContentTypes =
        ["image/jpeg", "image/png", "image/jpg", "application/pdf"];

    private static readonly string[] AllowedFileTypes =
        ["degree", "certificate", "id_card", "other"];

    public ExpertService(
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IConnectionMultiplexer redis,
        ILogger<ExpertService> logger)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
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
            Status = "pending",
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

        if (verification.Status == "approved")
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
        var urlSigner = BuildUrlSigner();

        var tasks = verifications.Select(async verification =>
        {
            var signedUrl = await GenerateSignedUrlAsync(urlSigner, verification.FileUrl);
            return MapToStaffDto(verification, signedUrl);
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    public async Task<ExpertVerificationStaffDto> GetVerificationDetailAsync(string verificationCode)
    {
        var verification = await _unitOfWork.StaffRepository.GetVerificationByCodeAsync(verificationCode)
            ?? throw new KeyNotFoundException($"Verification '{verificationCode}' không tồn tại");

        var urlSigner = BuildUrlSigner();
        var signedUrl = await GenerateSignedUrlAsync(urlSigner, verification.FileUrl);

        return MapToStaffDto(verification, signedUrl);
    }

    public async Task ReviewVerificationAsync(int staffId, string verificationCode, ReviewVerificationRequestDto request)
    {
        if (!request.Approved && string.IsNullOrWhiteSpace(request.RejectionReason))
            throw new InvalidOperationException("Phải cung cấp lý do từ chối khi Reject hồ sơ");

        var verification = await _unitOfWork.StaffRepository.GetVerificationByCodeAsync(verificationCode)
            ?? throw new KeyNotFoundException($"Verification '{verificationCode}' không tồn tại");

        if (verification.Status != "pending")
            throw new InvalidOperationException($"Hồ sơ này đã được xử lý với trạng thái '{verification.Status}'");

        verification.Status = request.Approved ? "approved" : "rejected";
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

    private UrlSigner BuildUrlSigner()
    {
        var keyFilePath = _configuration["GCS:ServiceAccountKeyPath"]
            ?? throw new InvalidOperationException("GCS ServiceAccountKeyPath not configured");

        // Resolve relative path từ ContentRootPath (= thư mục project WebAPI, không phải bin/Debug)
        var absoluteKeyPath = Path.IsPathRooted(keyFilePath)
            ? keyFilePath
            : Path.GetFullPath(Path.Combine(_hostEnvironment.ContentRootPath, keyFilePath));

        #pragma warning disable CS0618
        var credential = GoogleCredential.FromFile(absoluteKeyPath)
            .CreateScoped("https://www.googleapis.com/auth/devstorage.read_only");
        #pragma warning restore CS0618

        return UrlSigner.FromCredential(credential);
    }

    private async Task<string> GenerateSignedUrlAsync(UrlSigner urlSigner, string gcsPath)
    {
        var bucketName = _configuration["GCS:BucketName"]!;
        var objectName = gcsPath.Replace($"gs://{bucketName}/", "");

        return await urlSigner.SignAsync(
            bucketName,
            objectName,
            TimeSpan.FromMinutes(15),
            HttpMethod.Get);
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

    private static ExpertVerificationStaffDto MapToStaffDto(ExpertVerifications verification, string signedUrl)
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
            SignedUrl = signedUrl
        };
    }
}
