namespace EduVi.Contracts.Common;

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

    /// <summary>
    /// Đang tạo slide presentation
    /// </summary>
    public const int GeneratingSlides = 4;

    /// <summary>
    /// Slide presentation đã được tạo xong
    /// </summary>
    public const int SlidesGenerated = 5;

    /// <summary>
    /// Quá trình tạo slide thất bại
    /// </summary>
    public const int SlidesFailed = 6;

    /// <summary>
    /// Teacher đã xóa product (soft delete) — không hiển thị, không xử lý thêm
    /// </summary>
    public const int Deleted = 7;

    public static string GetStatusName(int? status) => status switch
    {
        New => "New",
        Processing => "Processing",
        Evaluated => "Evaluated",
        Failed => "Failed",
        GeneratingSlides => "Generating slides",
        SlidesGenerated => "Slides generated",
        SlidesFailed => "Slides generation failed",
        Deleted => "Deleted",
        _ => "Unknown"
    };
}
