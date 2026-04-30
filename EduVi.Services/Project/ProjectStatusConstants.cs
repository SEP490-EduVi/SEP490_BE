namespace EduVi.Services.Project;

/// <summary>
/// Hằng số trạng thái cho Project.
/// </summary>
public static class ProjectStatusConstants
{
    public const int Active = 0;
    public const int Deleted = 7;

    public static string GetStatusName(int? status) => status switch
    {
        Active => "Hoạt động",
        Deleted => "Đã xóa",
        _ => "Không xác định"
    };
}