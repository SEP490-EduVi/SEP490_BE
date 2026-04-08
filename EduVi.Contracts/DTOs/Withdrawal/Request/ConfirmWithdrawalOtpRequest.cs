using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Withdrawal.Request;

public class ConfirmWithdrawalOtpRequest
{
    [Required]
    public string BankAccountNumber { get; set; }

    [Required]
    public string BankName { get; set; }

    [Required]
    public string AccountHolderName { get; set; }

    [Required]
    [Range(200000, double.MaxValue, ErrorMessage = "Số tiền rút tối thiểu là 200,000 VND")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Mã OTP không được để trống")]
    public string OtpCode { get; set; }
}
