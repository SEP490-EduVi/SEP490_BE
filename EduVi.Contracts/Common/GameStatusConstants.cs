namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số trạng thái cho game đã generate.
/// </summary>
public static class GameStatusConstants
{
    public const int Queued = 0;
    public const int Completed = 1;
    public const int Failed = 2;
    public const int Deleted = 3;

    public static string GetStatusName(int status)
    {
        return status switch
        {
            Queued => "Queued",
            Completed => "Completed",
            Failed => "Failed",
            Deleted => "Deleted",
            _ => "Unknown"
        };
    }
}
