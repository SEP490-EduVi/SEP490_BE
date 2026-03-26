namespace EduVi.Services.Pipeline;

/// <summary>
/// Hằng số trạng thái cho ProductVideo pipeline.
/// </summary>
public static class VideoStatusConstants
{
    public const int Queued = 0;
    public const int Completed = 1;
    public const int Failed = 2;
    public const int Deleted = 3;

    public static string GetStatusName(int status)
    {
        return status switch
        {
            Queued => "queued",
            Completed => "completed",
            Failed => "failed",
            Deleted => "deleted",
            _ => "unknown"
        };
    }
}