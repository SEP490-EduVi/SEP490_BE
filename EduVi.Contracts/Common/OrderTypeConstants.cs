namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số loại đơn hàng.
/// </summary>
public static class OrderTypeConstants
{
    public const string Plan = "PLAN";
    public const string Material = "MATERIAL";

    public static string GetDisplayName(string? orderType)
    {
        if (string.IsNullOrWhiteSpace(orderType))
            return "Unknown";

        return orderType.Trim().ToUpperInvariant() switch
        {
            Plan => "Mua gói",
            Material => "Mua học liệu",
            _ => "Unknown"
        };
    }
}