namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số nguồn học liệu trong ProductMaterials.
/// </summary>
public static class ProductMaterialSourceTypeConstants
{
    public const string Marketplace = "Marketplace";
    public const string Upload = "Upload";

    public static string GetDisplayName(string? sourceType)
    {
        if (string.IsNullOrWhiteSpace(sourceType))
            return "Không xác định";

        return sourceType.Trim().ToLowerInvariant() switch
        {
            "marketplace" => "Marketplace",
            "upload" => "Direct upload",
            _ => "Unknown"
        };
    }
}