using EduVi.Contracts.DTOs.Payment.Request;
using EduVi.Contracts.DTOs.Payment.Response;

namespace EduVi.Services.Payment;

public interface IPaymentService
{
    // ============ Gói Subscription ============
    
    /// <summary>
    /// Lấy danh sách gói subscription đang active
    /// </summary>
    Task<List<QuotaPlanResponse>> GetAllPlansAsync();

    // ============ Ví EduCoin ============
    
    /// <summary>
    /// Lấy thông tin ví (tự tạo nếu chưa có)
    /// </summary>
    Task<WalletResponse> GetWalletAsync(int userId);

    /// <summary>
    /// Lấy quota hiện tại của giáo viên đăng nhập.
    /// </summary>
    Task<UserQuotaResponse> GetUserQuotaAsync(int userId);

    // ============ Nạp tiền qua PayOS ============
    
    /// <summary>
    /// Tạo link thanh toán PayOS để nạp EduCoin
    /// </summary>
    Task<TopUpResponse> CreateTopUpAsync(int userId, TopUpRequest request);
    
    /// <summary>
    /// Xử lý webhook từ PayOS khi thanh toán hoàn tất.
    /// Idempotent - gọi nhiều lần cùng orderCode vẫn chỉ cộng 1 lần.
    /// </summary>
    Task<bool> HandlePayOSWebhookAsync(Net.payOS.Types.WebhookType webhookBody);

    /// <summary>
    /// Xử lý return URL - kiểm tra trạng thái giao dịch sau khi user quay lại
    /// </summary>
    Task<TransactionHistoryResponse?> VerifyTopUpByOrderCodeAsync(long orderCode);

    /// <summary>
    /// Tự động hủy các giao dịch TOP_UP đang pending quá thời gian cho phép.
    /// </summary>
    Task<int> AutoCancelExpiredPendingTopUpsAsync(int timeoutMinutes, int batchSize, CancellationToken cancellationToken = default);

    // ============ Mua gói bằng EduCoin ============
    
    /// <summary>
    /// Trừ EduCoin trong ví → cộng quota cho Teacher
    /// </summary>
    Task<BuySubscriptionResponse> BuySubscriptionAsync(int userId, BuySubscriptionRequest request);

    // ============ Lịch sử giao dịch ============
    
    /// <summary>
    /// Lấy lịch sử giao dịch ví (paging)
    /// </summary>
    Task<(List<TransactionHistoryResponse> Items, int TotalCount)> GetTransactionHistoryAsync(
        int userId, int page, int pageSize);
}
