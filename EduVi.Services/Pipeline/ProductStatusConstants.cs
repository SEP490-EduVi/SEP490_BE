namespace EduVi.Services.Pipeline;

/// <summary>
/// Hằng số trạng thái cho Pipeline / Product evaluation
/// </summary>
public static class ProductStatusConstants
{
    /// <summary>
    /// Product vừa được tạo, đang chờ AI worker xử lý
    /// </summary>
    public const int New = 0;

    /// <summary>
    /// Đang được AI worker xử lý
    /// </summary>
    public const int Processing = 1;

    /// <summary>
    /// AI worker đã đánh giá xong
    /// </summary>
    public const int Evaluated = 2;

    /// <summary>
    /// Quá trình đánh giá thất bại
    /// </summary>
    public const int Failed = 3;

    public static string GetStatusName(int? status) => status switch
    {
        New => "NEW",
        Processing => "PROCESSING",
        Evaluated => "EVALUATED",
        Failed => "FAILED",
        _ => "UNKNOWN"
    };
}
