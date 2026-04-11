using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

/// <summary>
/// Repository xử lý Payment: Wallet, WalletTransactions, Orders, SubscriptionPlans, UserQuotas
/// </summary>
public interface IPaymentRepository
{
    // ============ Wallet ============
    Task<Wallets?> GetWalletByUserIdAsync(int userId);
    Task<Wallets?> GetWalletByIdAsync(int walletId);
    Task<Wallets> CreateWalletAsync(Wallets wallet);
    Task UpdateWalletBalanceAsync(int walletId, decimal newBalance);

    // ============ WalletTransactions ============
    
    /// <summary>
    /// Kiểm tra OrderCode đã tồn tại và COMPLETED chưa → chống cộng tiền 2 lần
    /// </summary>
    Task<bool> IsOrderCodeCompletedAsync(long orderCode);
    
    /// <summary>
    /// Lấy transaction theo OrderCode (dùng khi webhook callback)
    /// </summary>
    Task<WalletTransactions?> GetTransactionByOrderCodeAsync(long orderCode);
    
    Task<WalletTransactions> CreateTransactionAsync(WalletTransactions transaction);
    Task UpdateTransactionAsync(WalletTransactions transaction);
    
    /// <summary>
    /// Lấy lịch sử giao dịch của ví (phân trang)
    /// </summary>
    Task<(List<WalletTransactions> Items, int TotalCount)> GetTransactionsByWalletIdAsync(
        int walletId, int page, int pageSize);

    // ============ SubscriptionPlans ============
    Task<List<SubscriptionPlans>> GetAllActivePlansAsync();
    Task<SubscriptionPlans?> GetPlanByIdAsync(int planId);

    // ============ Orders ============
    Task<Orders> CreateOrderAsync(Orders order);

    // ============ Materials ============
    Task<Materials?> GetMaterialByCodeAsync(string materialCode);
    Task<bool> HasTeacherPurchasedMaterialAsync(int teacherId, int materialId);
    Task CreateTeacherMaterialAsync(TeacherMaterials teacherMaterial);

    // ============ UserQuotas ============
    Task<UserQuotas?> GetQuotaByTeacherIdAsync(int teacherId);
    Task<UserQuotas> CreateOrUpdateQuotaAsync(int teacherId, int analysisQuotaToAdd, int slideQuotaToAdd, int videoQuotaToAdd, int gameQuotaToAdd = 0);
    Task<bool> ConsumeAnalysisQuotaAsync(int teacherId, int amount = 1);
    Task<bool> ConsumeSlideQuotaAsync(int teacherId, int amount = 1);
    Task<bool> ConsumeVideoQuotaAsync(int teacherId, int amount = 1);
    Task<bool> ConsumeGameQuotaAsync(int teacherId, int amount = 1);
}
