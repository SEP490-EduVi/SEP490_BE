using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Withdrawal.Request;

public class AdminProcessWithdrawalRequest
{
    [Required]
    public bool Approved { get; set; }

    [MaxLength(500)]
    public string? AdminNote { get; set; }
}
