using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IWithdrawalRepository
{
    Task<WithdrawalRequests> CreateAsync(WithdrawalRequests request);

    Task<WithdrawalRequests?> GetByIdAsync(int withdrawalId);

    /// <summary>
    /// Danh sách yêu cầu rút tiền của 1 user (phân trang)
    /// </summary>
    Task<(List<WithdrawalRequests> Items, int TotalCount)> GetByUserIdAsync(
        int userId, int page, int pageSize);

    /// <summary>
    /// Admin: danh sách tất cả yêu cầu, có thể lọc theo status (phân trang)
    /// </summary>
    Task<(List<WithdrawalRequests> Items, int TotalCount)> GetAllAsync(
        int? status, int page, int pageSize);

    Task UpdateAsync(WithdrawalRequests request);
}
