namespace EduVi.Services.Pipeline;

/// <summary>
/// Hằng số trạng thái cho ProductVideo pipeline.
/// </summary>
public static class VideoStatusConstants
{
    public const string Queued = "queued";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Deleted = "deleted";
}