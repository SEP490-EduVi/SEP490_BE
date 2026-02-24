using Net.payOS;
using Net.payOS.Types;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace EduVi.Services.Payment;

/// <summary>
/// PayOS SDK wrapper — Net.payOS v1
/// </summary>
public class PayOSService : IPayOSService
{
    private readonly PayOS _payOS;
    private readonly ILogger<PayOSService> _logger;

    public PayOSService(IConfiguration configuration, ILogger<PayOSService> logger)
    {
        _logger = logger;

        var clientId = configuration["PayOS:ClientId"]
            ?? throw new InvalidOperationException("PayOS:ClientId is missing");
        var apiKey = configuration["PayOS:ApiKey"]
            ?? throw new InvalidOperationException("PayOS:ApiKey is missing");
        var checksumKey = configuration["PayOS:ChecksumKey"]
            ?? throw new InvalidOperationException("PayOS:ChecksumKey is missing");

        _payOS = new PayOS(clientId, apiKey, checksumKey);
        _logger.LogInformation("PayOS initialized");
    }

    public async Task<CreatePaymentResult> CreatePaymentLinkAsync(PaymentData paymentData)
    {
        var result = await _payOS.createPaymentLink(paymentData);
        _logger.LogInformation("PayOS link created. OrderCode={OrderCode}", paymentData.orderCode);
        return result;
    }

    public async Task<PaymentLinkInformation> GetPaymentLinkInfoAsync(long orderCode)
    {
        var result = await _payOS.getPaymentLinkInformation(orderCode);
        _logger.LogDebug("PayOS info retrieved. OrderCode={OrderCode}, Status={Status}", orderCode, result.status);
        return result;
    }

    public async Task<PaymentLinkInformation> CancelPaymentLinkAsync(long orderCode, string? reason = null)
    {
        var result = await _payOS.cancelPaymentLink(orderCode, reason);
        _logger.LogInformation("PayOS cancelled. OrderCode={OrderCode}", orderCode);
        return result;
    }

    public WebhookData VerifyPaymentWebhookData(WebhookType webhookBody)
    {
        return _payOS.verifyPaymentWebhookData(webhookBody);
    }
}
