using EduVi.Contracts.DTOs.Withdrawal.Request;
using EduVi.Contracts.DTOs.Withdrawal.Response;

namespace EduVi.Services.Withdrawal;

public interface IWithdrawalService
{
    /// <summary>
    /// Step 3: Validate input và gửi OTP qua email — chưa tạo request.
    /// </summary>
    Task SendWithdrawalOtpAsync(int userId, InitiateWithdrawalRequest request);

    /// <summary>
    /// Step 4+5: Xác nhận OTP → tạo WithdrawalRequest, freeze tiền trong ví.
    /// </summary>
    Task<WithdrawalResponse> ConfirmWithdrawalAsync(int userId, ConfirmWithdrawalOtpRequest request);

    /// <summary>
    /// User xem lịch sử yêu cầu rút tiền của chính mình.
    /// </summary>
    Task<(List<WithdrawalResponse> Items, int TotalCount)> GetMyWithdrawalsAsync(
        int userId, int page, int pageSize);

    /// <summary>
    /// Admin: xem tất cả yêu cầu, lọc theo status.
    /// </summary>
    Task<(List<AdminWithdrawalResponse> Items, int TotalCount)> GetAllWithdrawalsAsync(
        string? status, int page, int pageSize);

    /// <summary>
    /// Step 7+8: Admin duyệt hoặc từ chối — trừ tiền thật (SUCCESS) hoặc unlock (REJECTED).
    /// </summary>
    Task<WithdrawalResponse> ProcessWithdrawalAsync(int adminUserId, int withdrawalId, AdminProcessWithdrawalRequest request);
}
