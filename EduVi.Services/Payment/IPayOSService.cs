using Net.payOS.Types;

namespace EduVi.Services.Payment;

/// <summary>
/// Wrapper interface cho PayOS SDK — tách riêng để dễ mock khi test.
/// </summary>
public interface IPayOSService
{
    Task<CreatePaymentResult> CreatePaymentLinkAsync(PaymentData paymentData);
    Task<PaymentLinkInformation> GetPaymentLinkInfoAsync(long orderCode);
    Task<PaymentLinkInformation> CancelPaymentLinkAsync(long orderCode, string? reason = null);
    WebhookData VerifyPaymentWebhookData(WebhookType webhookBody);
}
