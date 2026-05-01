namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số trạng thái cho Project.
/// </summary>
public static class ProjectStatusConstants
{
    public const int Active = 0;
    public const int Deleted = 7;

    public static string GetStatusName(int? status) => status switch
    {
        Active => "ACTIVE",
        Deleted => "DELETED",
        _ => "UNKNOWN"
    };
}