using EduVi.Services.Payment;

namespace EduVi.WebAPI.BackgroundServices;

/// <summary>
/// Tự động xử lý các giao dịch TOP_UP bị treo ở trạng thái PENDING quá lâu.
/// </summary>
public class PaymentPendingTopUpAutoCancelBackgroundService : BackgroundService
{
    private readonly ILogger<PaymentPendingTopUpAutoCancelBackgroundService> _logger;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly bool _isEnabled;
    private readonly int _timeoutMinutes;
    private readonly int _batchSize;
    private readonly TimeSpan _scanInterval;

    public PaymentPendingTopUpAutoCancelBackgroundService(
        ILogger<PaymentPendingTopUpAutoCancelBackgroundService> logger,
        IServiceScopeFactory serviceScopeFactory,
        IConfiguration configuration)
    {
        _logger = logger;
        _serviceScopeFactory = serviceScopeFactory;

        var paymentAutoCancelSection = configuration.GetSection("Payment:PendingTopUpAutoCancel");

        _isEnabled = TryParseBool(paymentAutoCancelSection["Enabled"], defaultValue: true);
        _timeoutMinutes = TryParsePositiveInt(paymentAutoCancelSection["TimeoutMinutes"], defaultValue: 15);
        _batchSize = TryParsePositiveInt(paymentAutoCancelSection["BatchSize"], defaultValue: 100);

        var scanIntervalSeconds = TryParsePositiveInt(paymentAutoCancelSection["ScanIntervalSeconds"], defaultValue: 60);
        _scanInterval = TimeSpan.FromSeconds(scanIntervalSeconds);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_isEnabled)
        {
            _logger.LogInformation("Payment pending auto-cancel background service is disabled by configuration.");
            return;
        }

        _logger.LogInformation(
            "Payment pending auto-cancel background service started. TimeoutMinutes={TimeoutMinutes}, BatchSize={BatchSize}, ScanIntervalSeconds={ScanIntervalSeconds}",
            _timeoutMinutes,
            _batchSize,
            _scanInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();

                var cancelledCount = await paymentService.AutoCancelExpiredPendingTopUpsAsync(
                    _timeoutMinutes,
                    _batchSize,
                    stoppingToken);

                if (cancelledCount > 0)
                {
                    _logger.LogInformation(
                        "Auto-cancelled {CancelledCount} expired pending TOP_UP transaction(s).",
                        cancelledCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment auto-timeout cancel cycle failed.");
            }

            try
            {
                await Task.Delay(_scanInterval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Payment pending auto-cancel background service stopped.");
    }

    private static int TryParsePositiveInt(string? value, int defaultValue)
    {
        return int.TryParse(value, out var parsedValue) && parsedValue > 0
            ? parsedValue
            : defaultValue;
    }

    private static bool TryParseBool(string? value, bool defaultValue)
    {
        return bool.TryParse(value, out var parsedValue)
            ? parsedValue
            : defaultValue;
    }
}
