using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Payment.Request;

/// <summary>
/// Request mua gói subscription bằng EduCoin trong ví
/// </summary>
public class BuySubscriptionRequest
{
    /// <summary>
    /// ID gói subscription muốn mua
    /// </summary>
    [Required(ErrorMessage = "PlanId is required")]
    public int PlanId { get; set; }
}
