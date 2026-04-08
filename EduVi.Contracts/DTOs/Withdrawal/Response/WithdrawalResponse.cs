namespace EduVi.Contracts.DTOs.Withdrawal.Response;

public class WithdrawalResponse
{
    public int WithdrawalId { get; set; }
    public decimal Amount { get; set; }
    public decimal LockedAmount { get; set; }
    public string BankAccountNumber { get; set; }
    public string BankName { get; set; }
    public string AccountHolderName { get; set; }
    public string Status { get; set; }
    public string? AdminNote { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
