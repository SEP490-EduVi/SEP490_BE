using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Withdrawal.Request;

public class InitiateWithdrawalRequest
{
    [Required(ErrorMessage = "Số tài khoản ngân hàng không được để trống")]
    [MaxLength(30)]
    public string BankAccountNumber { get; set; }

    [Required(ErrorMessage = "Tên ngân hàng không được để trống")]
    [MaxLength(100)]
    public string BankName { get; set; }

    [Required(ErrorMessage = "Tên chủ tài khoản không được để trống")]
    [MaxLength(100)]
    public string AccountHolderName { get; set; }

    [Required(ErrorMessage = "Số tiền không được để trống")]
    [Range(200000, double.MaxValue, ErrorMessage = "Số tiền rút tối thiểu là 200,000 VND")]
    public decimal Amount { get; set; }
}
