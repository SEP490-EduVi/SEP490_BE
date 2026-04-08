using EduVi.Contracts.DTOs.Withdrawal.Request;
using EduVi.Contracts.DTOs.Withdrawal.Response;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using EduVi.Services.Email;
using EduVi.Services.Otp;
using EduVi.Services.RateLimit;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Withdrawal;

public class WithdrawalService : IWithdrawalService
{
    private const string OtpKeyPrefix = "otp:withdrawal:";
    private const decimal MinWithdrawalAmount = 200_000m;
    private const int RateLimitMaxRequests = 5;
    private const int RateLimitWindowMinutes = 5;

    private readonly IUnitOfWork _unitOfWork;
    private readonly IOtpService _otpService;
    private readonly IEmailService _emailService;
    private readonly IRateLimitService _rateLimitService;
    private readonly ILogger<WithdrawalService> _logger;

    public WithdrawalService(
        IUnitOfWork unitOfWork,
        IOtpService otpService,
        IEmailService emailService,
        IRateLimitService rateLimitService,
        ILogger<WithdrawalService> logger)
    {
        _unitOfWork = unitOfWork;
        _otpService = otpService;
        _emailService = emailService;
        _rateLimitService = rateLimitService;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Step 3: Validate + gửi OTP (chưa tạo WithdrawalRequest)
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task SendWithdrawalOtpAsync(int userId, InitiateWithdrawalRequest request)
    {
        // Rate limiting: max 5 lần / 5 phút
        var rateLimitKey = $"withdrawal:otp:{userId}";
        var allowed = await _rateLimitService.IsAllowedAsync(rateLimitKey, RateLimitMaxRequests, RateLimitWindowMinutes);
        if (!allowed)
            throw new InvalidOperationException($"Quá nhiều yêu cầu. Vui lòng thử lại sau {RateLimitWindowMinutes} phút.");

        if (request.Amount < MinWithdrawalAmount)
            throw new InvalidOperationException($"Số tiền rút tối thiểu là {MinWithdrawalAmount:N0} VND.");

        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new InvalidOperationException("Người dùng không tồn tại.");

        var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(userId)
            ?? throw new InvalidOperationException("Bạn chưa có ví. Vui lòng nạp tiền trước.");

        var availableBalance = (wallet.Balance ?? 0);
        if (availableBalance < request.Amount)
            throw new InvalidOperationException(
                $"Số dư không đủ. Số dư hiện tại: {availableBalance:N0} VND, yêu cầu rút: {request.Amount:N0} VND.");

        var otp = _otpService.GenerateOtp();
        await _otpService.SaveOtpAsync(userId, otp, ttlMinutes: 5, keyPrefix: OtpKeyPrefix);

        await _emailService.SendWithdrawalOtpEmailAsync(user.Email, user.FullName ?? user.Username, otp, request.Amount);

        _logger.LogInformation("Withdrawal OTP sent. UserId={UserId}, Amount={Amount}", userId, request.Amount);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Step 4+5: Xác nhận OTP → tạo WithdrawalRequest + freeze tiền
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<WithdrawalResponse> ConfirmWithdrawalAsync(int userId, ConfirmWithdrawalOtpRequest request)
    {
        if (request.Amount < MinWithdrawalAmount)
            throw new InvalidOperationException($"Số tiền rút tối thiểu là {MinWithdrawalAmount:N0} VND.");

        // Xác minh OTP
        var otpValid = await _otpService.VerifyOtpAsync(userId, request.OtpCode, keyPrefix: OtpKeyPrefix);
        if (!otpValid)
            throw new InvalidOperationException("Mã OTP không hợp lệ hoặc đã hết hạn.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(userId)
                ?? throw new InvalidOperationException("Ví không tồn tại.");

            var availableBalance = wallet.Balance ?? 0;
            if (availableBalance < request.Amount)
                throw new InvalidOperationException(
                    $"Số dư không đủ. Số dư hiện tại: {availableBalance:N0} VND, yêu cầu rút: {request.Amount:N0} VND.");

            // Freeze tiền: trừ ngay khỏi Balance để user không thể tiêu tiếp
            await _unitOfWork.PaymentRepository.UpdateWalletBalanceAsync(wallet.WalletId, availableBalance - request.Amount);

            // Tạo WithdrawalRequest với CONFIRMED
            var withdrawalRequest = new WithdrawalRequests
            {
                UserId = userId,
                Amount = request.Amount,
                LockedAmount = request.Amount,
                BankAccountNumber = request.BankAccountNumber,
                BankName = request.BankName,
                AccountHolderName = request.AccountHolderName,
                Status = WithdrawalStatus.Confirmed,
                CreatedAt = DateTime.UtcNow
            };
            await _unitOfWork.WithdrawalRepository.CreateAsync(withdrawalRequest);

            await _unitOfWork.CommitTransactionAsync();

            // Xóa OTP sau khi dùng thành công
            await _otpService.RevokeOtpAsync(userId, keyPrefix: OtpKeyPrefix);

            _logger.LogInformation("Withdrawal confirmed. UserId={UserId}, WithdrawalId={WithdrawalId}, Amount={Amount}",
                userId, withdrawalRequest.WithdrawalId, request.Amount);

            return MapToResponse(withdrawalRequest);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // User: xem lịch sử yêu cầu rút tiền
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<(List<WithdrawalResponse> Items, int TotalCount)> GetMyWithdrawalsAsync(
        int userId, int page, int pageSize)
    {
        var (items, totalCount) = await _unitOfWork.WithdrawalRepository.GetByUserIdAsync(userId, page, pageSize);
        return (items.Select(MapToResponse).ToList(), totalCount);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Admin: xem tất cả yêu cầu
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<(List<AdminWithdrawalResponse> Items, int TotalCount)> GetAllWithdrawalsAsync(
        int? status, int page, int pageSize)
    {
        var (items, totalCount) = await _unitOfWork.WithdrawalRepository.GetAllAsync(status, page, pageSize);
        return (items.Select(MapToAdminResponse).ToList(), totalCount);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Step 7+8: Admin duyệt hoặc từ chối
    // ─────────────────────────────────────────────────────────────────────────────

    public async Task<WithdrawalResponse> ProcessWithdrawalAsync(
        int adminUserId, int withdrawalId, AdminProcessWithdrawalRequest request)
    {
        var withdrawal = await _unitOfWork.WithdrawalRepository.GetByIdAsync(withdrawalId)
            ?? throw new KeyNotFoundException($"Yêu cầu rút tiền #{withdrawalId} không tồn tại.");

        if (withdrawal.Status != WithdrawalStatus.Confirmed)
            throw new InvalidOperationException(
                $"Yêu cầu này đang ở trạng thái {WithdrawalStatus.GetStatusName(withdrawal.Status)}, không thể xử lý.");

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            if (request.Approved)
            {
                // SUCCESS: tiền đã bị freeze từ trước, chỉ cập nhật trạng thái
                // Ghi lại WalletTransaction để có lịch sử
                var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(withdrawal.UserId)
                    ?? throw new InvalidOperationException("Ví người dùng không tồn tại.");

                var balanceAfter = wallet.Balance ?? 0; // tiền đã bị freeze rồi
                var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100 + Random.Shared.Next(10, 99);

                await _unitOfWork.PaymentRepository.CreateTransactionAsync(new WalletTransactions
                {
                    WalletId = wallet.WalletId,
                    OrderCode = orderCode,
                    TransactionType = "WITHDRAWAL",
                    Amount = -withdrawal.LockedAmount,
                    BalanceBefore = balanceAfter + withdrawal.LockedAmount,
                    BalanceAfter = balanceAfter,
                    Status = 1, // COMPLETED
                    Description = $"Rút tiền tới {withdrawal.BankName} - {withdrawal.BankAccountNumber}",
                    CreatedAt = DateTime.UtcNow
                });

                withdrawal.Status = WithdrawalStatus.Success;
            }
            else
            {
                // REJECTED: hoàn lại tiền đã freeze
                var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(withdrawal.UserId)
                    ?? throw new InvalidOperationException("Ví người dùng không tồn tại.");

                await _unitOfWork.PaymentRepository.UpdateWalletBalanceAsync(
                    wallet.WalletId, (wallet.Balance ?? 0) + withdrawal.LockedAmount);

                withdrawal.Status = WithdrawalStatus.Rejected;
            }

            withdrawal.AdminNote = request.AdminNote;
            withdrawal.ProcessedByAdminId = adminUserId;
            withdrawal.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.WithdrawalRepository.UpdateAsync(withdrawal);

            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Withdrawal processed. WithdrawalId={WithdrawalId}, Approved={Approved}, AdminUserId={AdminUserId}",
                withdrawalId, request.Approved, adminUserId);

            return MapToResponse(withdrawal);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Mapping
    // ─────────────────────────────────────────────────────────────────────────────

    private static WithdrawalResponse MapToResponse(WithdrawalRequests withdrawal) => new()
    {
        WithdrawalId = withdrawal.WithdrawalId,
        Amount = withdrawal.Amount,
        LockedAmount = withdrawal.LockedAmount,
        BankAccountNumber = withdrawal.BankAccountNumber,
        BankName = withdrawal.BankName,
        AccountHolderName = withdrawal.AccountHolderName,
        Status = withdrawal.Status,
        AdminNote = withdrawal.AdminNote,
        CreatedAt = withdrawal.CreatedAt,
        UpdatedAt = withdrawal.UpdatedAt
    };

    private static AdminWithdrawalResponse MapToAdminResponse(WithdrawalRequests withdrawal) => new()
    {
        WithdrawalId = withdrawal.WithdrawalId,
        Amount = withdrawal.Amount,
        LockedAmount = withdrawal.LockedAmount,
        BankAccountNumber = withdrawal.BankAccountNumber,
        BankName = withdrawal.BankName,
        AccountHolderName = withdrawal.AccountHolderName,
        Status = withdrawal.Status,
        AdminNote = withdrawal.AdminNote,
        CreatedAt = withdrawal.CreatedAt,
        UpdatedAt = withdrawal.UpdatedAt,
        UserId = withdrawal.UserId,
        UserFullName = withdrawal.User?.FullName ?? "",
        UserEmail = withdrawal.User?.Email ?? ""
    };
}

/// <summary>Hằng số trạng thái WithdrawalRequest</summary>
public static class WithdrawalStatus
{
    public const int Pending = 0;
    public const int Confirmed = 1;
    public const int Success = 2;
    public const int Rejected = 3;

    public static string GetStatusName(int status) => status switch
    {
        Pending => "PENDING",
        Confirmed => "CONFIRMED",
        Success => "SUCCESS",
        Rejected => "REJECTED",
        _ => "UNKNOWN"
    };
}
