namespace EduVi.Contracts.DTOs.Withdrawal.Response;

public class AdminWithdrawalResponse : WithdrawalResponse
{
    public int UserId { get; set; }
    public string UserFullName { get; set; }
    public string UserEmail { get; set; }
}
