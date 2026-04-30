namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số loại giao dịch ví (WalletTransactions.TransactionType).
/// Database lưu code tiếng Anh, API có thể map sang tên hiển thị tiếng Việt.
/// </summary>
public static class WalletTransactionTypeConstants
{
    public const string TopUp = "TOP_UP";
    public const string BuySubscription = "BUY_SUBSCRIPTION";
    public const string BuyMaterial = "BUY_MATERIAL";
    public const string MaterialRevenue = "MATERIAL_REVENUE";
    public const string MaterialPlatformFee = "MATERIAL_PLATFORM_FEE";
    public const string MaterialAdminRevenue = "MATERIAL_ADMIN_REVENUE";
    public const string ClaimFreeMaterial = "CLAIM_FREE_MATERIAL";
    public const string Withdrawal = "WITHDRAWAL";

    public static readonly string[] AdminMaterialIncomeTransactionTypes =
    [
        MaterialPlatformFee,
        MaterialAdminRevenue
    ];

    public static string GetDisplayName(string? transactionType)
    {
        return transactionType switch
        {
            TopUp => "Nạp EduCoin",
            BuySubscription => "Mua gói cước",
            BuyMaterial => "Mua học liệu",
            MaterialRevenue => "Doanh thu học liệu chuyên gia",
            MaterialPlatformFee => "Phí nền tảng học liệu",
            MaterialAdminRevenue => "Doanh thu học liệu quản trị",
            ClaimFreeMaterial => "Nhận học liệu miễn phí",
            Withdrawal => "Rút tiền",
            _ => string.IsNullOrWhiteSpace(transactionType) ? "Không xác định" : transactionType
        };
    }
}