namespace EduVi.Contracts.Common;

/// <summary>
/// Hằng số loại học liệu.
/// Giá trị code giữ nguyên để tương thích dữ liệu hiện tại, tên hiển thị trả tiếng Việt.
/// </summary>
public static class MaterialTypeConstants
{
    public const string Image = "image";
    public const string Video = "video";
    public const string Audio = "audio";
    public const string Document = "document";
    public const string Quiz = "quiz";
    public const string Flashcard = "flashcard";
    public const string LessonPlan = "lesson_plan";

    public static string GetDisplayName(string? type)
    {
        var normalizedType = type?.Trim().ToLowerInvariant();
        return normalizedType switch
        {
            Image => "Hình ảnh",
            Video => "Video",
            Audio => "Âm thanh",
            Document => "Tài liệu",
            Quiz => "Câu đố",
            Flashcard => "Thẻ ghi nhớ",
            LessonPlan => "Giáo án",
            _ => "Không xác định"
        };
    }
}