namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số trạng thái duyệt học liệu.
/// </summary>
public static class MaterialApprovalStatusConstants
{
    public const int Pending = 0;
    public const int Approved = 1;
    public const int Rejected = 2;
    public const int Banned = 3;

    public static string GetStatusName(int? approvalStatus) => approvalStatus switch
    {
        Pending => "Đang chờ duyệt",
        Approved => "Đã duyệt",
        Rejected => "Bị từ chối",
        Banned => "Bị khóa",
        _ => "Không xác định"
    };
}