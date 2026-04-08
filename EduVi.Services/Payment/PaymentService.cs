using EduVi.Contracts.DTOs.Payment.Request;
using EduVi.Contracts.DTOs.Payment.Response;
using EduVi.Repositories.Interfaces;
using EduVi.Repositories.Models;
using Microsoft.Extensions.Logging;
using Net.payOS.Types;
using static EduVi.Services.Payment.PaymentConstants;

namespace EduVi.Services.Payment;

public class PaymentService : IPaymentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPayOSService _payOSService;
    private readonly ILogger<PaymentService> _logger;

    public PaymentService(IUnitOfWork unitOfWork, IPayOSService payOSService, ILogger<PaymentService> logger)
    {
        _unitOfWork = unitOfWork;
        _payOSService = payOSService;
        _logger = logger;
    }

    #region Gói Subscription

    public async Task<List<SubscriptionPlanResponse>> GetAllPlansAsync()
    {
        var plans = await _unitOfWork.PaymentRepository.GetAllActivePlansAsync();
        return plans.Select(MapToPlanResponse).ToList();
    }

    #endregion

    #region Ví EduCoin

    public async Task<WalletResponse> GetWalletAsync(int userId)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        return MapToWalletResponse(wallet);
    }

    public async Task<UserQuotaResponse> GetUserQuotaAsync(int userId)
    {
        var teacherId = await GetTeacherIdOrThrowAsync(userId);
        var quota = await _unitOfWork.PaymentRepository.GetQuotaByTeacherIdAsync(teacherId);

        if (quota == null)
        {
            return new UserQuotaResponse
            {
                TotalAnalysisQuota = 0,
                AvailableAnalysisQuota = 0,
                UsedAnalysisQuota = 0,
                TotalSlideQuota = 0,
                AvailableSlideQuota = 0,
                UsedSlideQuota = 0,
                TotalVideoQuota = 0,
                AvailableVideoQuota = 0,
                UsedVideoQuota = 0,
                UpdatedAt = null
            };
        }

        return MapToUserQuotaResponse(quota);
    }

    #endregion

    #region Nạp tiền qua PayOS

    public async Task<TopUpResponse> CreateTopUpAsync(int userId, TopUpRequest request)
    {
        var wallet = await GetOrCreateWalletAsync(userId);
        var orderCode = await GenerateUniqueOrderCodeAsync();

        // Lưu transaction PENDING ngay (cần persist trước khi gọi PayOS)
        var transaction = new WalletTransactions
        {
            WalletId = wallet.WalletId,
            OrderCode = orderCode,
            TransactionType = TransactionType.TopUp,
            Amount = request.Amount,
            BalanceBefore = wallet.Balance ?? 0,
            Status = Status.Pending,
            Description = request.Description ?? $"Nạp {request.Amount:N0} EduCoin",
            CreatedAt = DateTime.UtcNow
        };
        await _unitOfWork.PaymentRepository.CreateTransactionAsync(transaction);
        await _unitOfWork.SaveChangesAsync(); // Persist PENDING record ngay

        try
        {
            var paymentData = new PaymentData(
                orderCode: orderCode,
                amount: (int)request.Amount,
                description: $"Nap {request.Amount} EduCoin",
                items: [new ItemData("EduCoin TopUp", 1, (int)request.Amount)],
                returnUrl: request.ReturnUrl,
                cancelUrl: request.CancelUrl
            );

            var result = await _payOSService.CreatePaymentLinkAsync(paymentData);

            _logger.LogInformation("TopUp link created. OrderCode={OrderCode}, Amount={Amount}", orderCode, request.Amount);

            return new TopUpResponse
            {
                OrderCode = orderCode,
                CheckoutUrl = result.checkoutUrl,
                Amount = request.Amount,
                Status = GetStatusName(Status.Pending)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PayOS createPaymentLink failed. OrderCode={OrderCode}", orderCode);
            await FailTransactionAsync(transaction, $"PayOS error: {ex.Message}");
            throw new InvalidOperationException("Không thể tạo link thanh toán. Vui lòng thử lại sau.", ex);
        }
    }

    /// <summary>
    /// Xử lý webhook từ PayOS. Idempotent — orderCode đã COMPLETED thì bỏ qua.
    /// </summary>
    public async Task<bool> HandlePayOSWebhookAsync(WebhookType webhookBody)
    {
        WebhookData data;
        try
        {
            data = _payOSService.VerifyPaymentWebhookData(webhookBody);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Invalid PayOS webhook signature");
            return false;
        }

        var orderCode = data.orderCode;
        _logger.LogInformation("Webhook received. OrderCode={OrderCode}, Code={Code}", orderCode, data.code);

        // Idempotency: đã hoàn thành thì trả OK luôn
        if (await _unitOfWork.PaymentRepository.IsOrderCodeCompletedAsync(orderCode))
        {
            _logger.LogWarning("Duplicate webhook. OrderCode={OrderCode} already completed", orderCode);
            return true;
        }

        var transaction = await _unitOfWork.PaymentRepository.GetTransactionByOrderCodeAsync(orderCode);
        if (transaction == null)
        {
            _logger.LogWarning("Transaction not found. OrderCode={OrderCode}", orderCode);
            return false;
        }

        if (data.code == PayOSCode.Success)
        {
            await CompleteTopUpWithTransactionAsync(transaction);
            _logger.LogInformation("TopUp completed. OrderCode={OrderCode}, Amount={Amount}", orderCode, transaction.Amount);
        }
        else
        {
            var newStatus = data.code == PayOSCode.Cancelled ? Status.Cancelled : Status.Failed;
            await FailTransactionAsync(transaction, $"PayOS: {data.desc}", newStatus);
            _logger.LogInformation("TopUp {Status}. OrderCode={OrderCode}", GetStatusName(newStatus), orderCode);
        }

        return true;
    }

    /// <summary>
    /// FE gọi sau khi redirect về — kiểm tra trạng thái thực từ PayOS.
    /// </summary>
    public async Task<TransactionHistoryResponse?> VerifyTopUpByOrderCodeAsync(long orderCode)
    {
        var transaction = await _unitOfWork.PaymentRepository.GetTransactionByOrderCodeAsync(orderCode);
        if (transaction == null) return null;

        if (transaction.Status == Status.Pending)
        {
            await SyncTransactionWithPayOS(transaction, orderCode);
        }

        // Re-fetch sau khi có thể đã update
        transaction = await _unitOfWork.PaymentRepository.GetTransactionByOrderCodeAsync(orderCode);
        return transaction == null ? null : MapToTransactionResponse(transaction);
    }

    #endregion

    #region Mua gói bằng EduCoin

    /// <summary>
    /// Atomic: BeginTransaction → re-read balance → trừ ví → tạo order → cộng quota → lưu transaction → Commit.
    /// Nếu bất kỳ bước nào fail → Rollback toàn bộ.
    /// </summary>
    public async Task<BuySubscriptionResponse> BuySubscriptionAsync(int userId, BuySubscriptionRequest request)
    {
        // Validate ngoài transaction (read-only)
        var plan = await _unitOfWork.PaymentRepository.GetPlanByIdAsync(request.PlanId)
            ?? throw new InvalidOperationException("Gói subscription không tồn tại hoặc đã ngưng bán.");

        var planPrice = plan.Price ?? 0;
        if (planPrice <= 0)
            throw new InvalidOperationException("Giá gói không hợp lệ.");

        var teacherId = await GetTeacherIdOrThrowAsync(userId);

        // === BẮT ĐẦU TRANSACTION ===
        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Re-read balance BÊN TRONG transaction để tránh race condition
            var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(userId)
                ?? throw new InvalidOperationException("Ví không tồn tại.");

            var currentBalance = wallet.Balance ?? 0;

            if (currentBalance < planPrice)
                throw new InvalidOperationException(
                    $"Số dư không đủ. Cần {planPrice:N0} EduCoin, hiện có {currentBalance:N0} EduCoin.");

            var orderCode = await GenerateUniqueOrderCodeAsync();
            var newBalance = currentBalance - planPrice;

            // Trừ tiền ví
            await _unitOfWork.PaymentRepository.UpdateWalletBalanceAsync(wallet.WalletId, newBalance);

            // Tạo Order
            var order = await _unitOfWork.PaymentRepository.CreateOrderAsync(new Orders
            {
                TeacherId = teacherId,
                TotalAmount = planPrice,
                OrderDate = DateTime.UtcNow,
                Status = Status.Completed,
                PaymentMethod = Method.EduCoin
            });

            // Cộng quota
            var analysisQuotaToAdd = plan.AnalysisQuotaAmount ?? 0;
            var slideQuotaToAdd = plan.SlideQuotaAmount ?? 0;
            var videoQuotaToAdd = plan.VideoQuotaAmount ?? 0;
            var updatedQuota = await _unitOfWork.PaymentRepository.CreateOrUpdateQuotaAsync(
                teacherId,
                analysisQuotaToAdd,
                slideQuotaToAdd,
                videoQuotaToAdd);

            // Lưu transaction lịch sử
            await _unitOfWork.PaymentRepository.CreateTransactionAsync(new WalletTransactions
            {
                WalletId = wallet.WalletId,
                OrderCode = orderCode,
                TransactionType = TransactionType.BuySubscription,
                Amount = -planPrice,
                BalanceBefore = currentBalance,
                BalanceAfter = newBalance,
                Status = Status.Completed,
                Description = $"Mua gói {plan.PlanName}",
                PlanId = plan.PlanId,
                CreatedAt = DateTime.UtcNow
            });

            // === COMMIT: SaveChanges + Commit cùng lúc ===
            await _unitOfWork.CommitTransactionAsync();

            _logger.LogInformation("Subscription purchased. UserId={UserId}, PlanId={PlanId}", userId, plan.PlanId);

            return new BuySubscriptionResponse
            {
                OrderId = order.OrderId,
                PlanName = plan.PlanName ?? "",
                Amount = planPrice,
                Status = GetStatusName(Status.Completed),
                AnalysisQuotaAdded = analysisQuotaToAdd,
                SlideQuotaAdded = slideQuotaToAdd,
                VideoQuotaAdded = videoQuotaToAdd,
                AvailableAnalysisQuotaAfter = updatedQuota.AvailableAnalysisQuota ?? 0,
                AvailableSlideQuotaAfter = updatedQuota.AvailableSlideQuota ?? 0,
                AvailableVideoQuotaAfter = updatedQuota.AvailableVideoQuota ?? 0,
                WalletBalanceAfter = newBalance,
                PurchasedAt = DateTime.UtcNow
            };
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    #endregion

    #region Lịch sử giao dịch

    public async Task<(List<TransactionHistoryResponse> Items, int TotalCount)> GetTransactionHistoryAsync(
        int userId, int page, int pageSize)
    {
        var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(userId);
        if (wallet == null)
            return ([], 0);

        var (items, totalCount) = await _unitOfWork.PaymentRepository
            .GetTransactionsByWalletIdAsync(wallet.WalletId, page, pageSize);

        return (items.Select(MapToTransactionResponse).ToList(), totalCount);
    }

    #endregion

    #region Private Helpers

    private async Task<Wallets> GetOrCreateWalletAsync(int userId)
    {
        var wallet = await _unitOfWork.PaymentRepository.GetWalletByUserIdAsync(userId);
        if (wallet != null) return wallet;

        wallet = await _unitOfWork.PaymentRepository.CreateWalletAsync(new Wallets
        {
            UserId = userId,
            Balance = 0,
            LastUpdated = DateTime.UtcNow
        });
        await _unitOfWork.SaveChangesAsync();
        return wallet;
    }

    /// <summary>
    /// Cộng tiền vào ví BÊN TRONG transaction.
    /// Double-check idempotency bên trong transaction để chống race condition
    /// giữa webhook và VerifyTopUp chạy đồng thời.
    /// </summary>
    private async Task CompleteTopUpWithTransactionAsync(WalletTransactions transaction)
    {
        if (transaction.WalletId == null || transaction.Amount == null) return;

        var walletId = transaction.WalletId.Value;
        var amount = transaction.Amount.Value;

        await _unitOfWork.BeginTransactionAsync();
        try
        {
            // Double-check idempotency BÊN TRONG transaction
            if (await _unitOfWork.PaymentRepository.IsOrderCodeCompletedAsync(transaction.OrderCode))
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogWarning("CompleteTopUp skipped — already completed. OrderCode={OrderCode}", transaction.OrderCode);
                return;
            }

            // Re-fetch wallet để lấy balance HIỆN TẠI (tránh stale data)
            var wallet = await _unitOfWork.PaymentRepository.GetWalletByIdAsync(walletId);
            if (wallet == null)
            {
                await _unitOfWork.RollbackTransactionAsync();
                return;
            }

            var currentBalance = wallet.Balance ?? 0;
            var newBalance = currentBalance + amount;

            await _unitOfWork.PaymentRepository.UpdateWalletBalanceAsync(walletId, newBalance);

            // Re-fetch transaction vì có thể đang bị tracked với data cũ
            var freshTx = await _unitOfWork.PaymentRepository.GetTransactionByOrderCodeAsync(transaction.OrderCode);
            if (freshTx != null)
            {
                freshTx.BalanceBefore = currentBalance;
                freshTx.BalanceAfter = newBalance;
                freshTx.Status = Status.Completed;
                freshTx.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.PaymentRepository.UpdateTransactionAsync(freshTx);
            }

            await _unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync();
            throw;
        }
    }

    /// <summary>
    /// Đánh dấu transaction thất bại / bị hủy và persist ngay.
    /// </summary>
    private async Task FailTransactionAsync(WalletTransactions transaction, string description, int status = Status.Failed)
    {
        transaction.Status = status;
        transaction.Description = description;
        transaction.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.PaymentRepository.UpdateTransactionAsync(transaction);
        await _unitOfWork.SaveChangesAsync();
    }

    /// <summary>
    /// Đồng bộ trạng thái transaction PENDING với PayOS (gọi khi FE redirect về).
    /// </summary>
    private async Task SyncTransactionWithPayOS(WalletTransactions transaction, long orderCode)
    {
        try
        {
            var info = await _payOSService.GetPaymentLinkInfoAsync(orderCode);

            if (info.status == PayOSStatus.Paid)
            {
                // Dùng CompleteTopUpWithTransactionAsync — đã có double-check idempotency bên trong
                await CompleteTopUpWithTransactionAsync(transaction);
            }
            else if (info.status == PayOSStatus.Cancelled)
            {
                await FailTransactionAsync(transaction, "Cancelled by user", Status.Cancelled);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncTransactionWithPayOS failed. OrderCode={OrderCode}", orderCode);
        }
    }

    private async Task<int> GetTeacherIdOrThrowAsync(int userId)
    {
        var user = await _unitOfWork.AuthenticationRepository.GetUserByIdAsync(userId)
            ?? throw new InvalidOperationException("Không tìm thấy người dùng.");

        return user.Teachers?.TeacherId
            ?? throw new InvalidOperationException("Chỉ giáo viên mới có thể mua gói subscription.");
    }

    /// <summary>
    /// Tạo orderCode unique với max retries (tránh infinite loop).
    /// </summary>
    private async Task<long> GenerateUniqueOrderCodeAsync()
    {
        const int maxRetries = 5;

        for (var i = 0; i < maxRetries; i++)
        {
            var orderCode = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 100 + Random.Shared.Next(10, 99);

            if (await _unitOfWork.PaymentRepository.GetTransactionByOrderCodeAsync(orderCode) == null)
                return orderCode;
        }

        throw new InvalidOperationException("Không thể tạo mã giao dịch. Vui lòng thử lại.");
    }

    #endregion

    #region Mapping

    private static SubscriptionPlanResponse MapToPlanResponse(SubscriptionPlans p) => new()
    {
        PlanId = p.PlanId,
        PlanName = p.PlanName ?? "",
        Price = p.Price ?? 0,
        DurationDays = p.DurationDays ?? 0,
        AnalysisQuotaAmount = p.AnalysisQuotaAmount ?? 0,
        SlideQuotaAmount = p.SlideQuotaAmount ?? 0,
        VideoQuotaAmount = p.VideoQuotaAmount ?? 0,
        Description = p.Description,
        IsActive = p.IsActive ?? false
    };

    private static WalletResponse MapToWalletResponse(Wallets w) => new()
    {
        WalletId = w.WalletId,
        UserId = w.UserId ?? 0,
        Balance = w.Balance ?? 0,
        LastUpdated = w.LastUpdated
    };

    private static UserQuotaResponse MapToUserQuotaResponse(UserQuotas quota) => new()
    {
        TotalAnalysisQuota = quota.TotalAnalysisQuota ?? 0,
        AvailableAnalysisQuota = quota.AvailableAnalysisQuota ?? 0,
        UsedAnalysisQuota = quota.UsedAnalysisQuota ?? 0,
        TotalSlideQuota = quota.TotalSlideQuota ?? 0,
        AvailableSlideQuota = quota.AvailableSlideQuota ?? 0,
        UsedSlideQuota = quota.UsedSlideQuota ?? 0,
        TotalVideoQuota = quota.TotalVideoQuota ?? 0,
        AvailableVideoQuota = quota.AvailableVideoQuota ?? 0,
        UsedVideoQuota = quota.UsedVideoQuota ?? 0,
        UpdatedAt = quota.UpdatedAt
    };

    private static TransactionHistoryResponse MapToTransactionResponse(WalletTransactions t) => new()
    {
        TransactionId = t.TransactionId,
        OrderCode = t.OrderCode,
        TransactionType = t.TransactionType ?? "",
        Amount = t.Amount ?? 0,
        BalanceBefore = t.BalanceBefore ?? 0,
        BalanceAfter = t.BalanceAfter ?? 0,
        Status = GetStatusName(t.Status),
        Description = t.Description,
        PlanName = t.Plan?.PlanName,
        MaterialTitle = t.Material?.Title,
        CreatedAt = t.CreatedAt ?? DateTime.MinValue
    };

    #endregion
}
