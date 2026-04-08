namespace EduVi.Services.Payment;

/// <summary>
/// Tập trung tất cả hằng số liên quan đến Payment, tránh magic number/string rải rác.
/// </summary>
public static class PaymentConstants
{
    /// <summary>
    /// Trạng thái giao dịch (WalletTransactions.Status)
    /// </summary>
    public static class Status
    {
        public const int Pending = 0;
        public const int Completed = 1;
        public const int Failed = 2;
        public const int Cancelled = 3;
    }

    /// <summary>
    /// Loại giao dịch (WalletTransactions.TransactionType)
    /// </summary>
    public static class TransactionType
    {
        public const string TopUp = "TOP_UP";
        public const string BuySubscription = "BUY_SUBSCRIPTION";
        public const string BuyMaterial = "BUY_MATERIAL";
    }

    /// <summary>
    /// Mã trả về từ PayOS webhook (WebhookData.code)
    /// </summary>
    public static class PayOSCode
    {
        public const string Success = "00";
        public const string Cancelled = "01";
    }

    /// <summary>
    /// Phương thức thanh toán (Orders.PaymentMethod)
    /// </summary>
    public static class Method
    {
        public const string EduCoin = "EDUCOIN";
    }

    /// <summary>
    /// Trạng thái payment link PayOS (PaymentLinkInformation.status)
    /// </summary>
    public static class PayOSStatus
    {
        public const string Paid = "PAID";
        public const string Cancelled = "CANCELLED";
    }

    /// <summary>
    /// Chuyển status int → string hiển thị
    /// </summary>
    public static string GetStatusName(int? status) => status switch
    {
        Status.Pending => "PENDING",
        Status.Completed => "COMPLETED",
        Status.Failed => "FAILED",
        Status.Cancelled => "CANCELLED",
        _ => "UNKNOWN"
    };
}
