namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số trạng thái cho TextbookDocument ingestion.
/// </summary>
public static class TextbookDocumentStatusConstants
{
    public const int Pending = 0;
    public const int Processing = 1;
    public const int Completed = 2;
    public const int Failed = 3;
    public const int Deleting = 4;
    public const int Deleted = 5;

    public static string GetStatusName(int? status) => status switch
    {
        Pending => "PENDING",
        Processing => "PROCESSING",
        Completed => "COMPLETED",
        Failed => "FAILED",
        Deleting => "DELETING",
        Deleted => "DELETED",
        _ => "UNKNOWN"
    };
}