namespace EduVi.Services.CurriculumIngestion;

/// <summary>
/// Hằng số trạng thái cho CurriculumDocument ingestion
/// </summary>
public static class CurriculumDocumentStatusConstants
{
    /// <summary>
    /// Tài liệu vừa upload, đang chờ Python worker xử lý
    /// </summary>
    public const int Pending = 0;

    /// <summary>
    /// Đang được Python worker xử lý (parsing, ingesting, mapping)
    /// </summary>
    public const int Processing = 1;

    /// <summary>
    /// Ingestion hoàn tất — dữ liệu đã được đưa vào Neo4j
    /// </summary>
    public const int Completed = 2;

    /// <summary>
    /// Quá trình ingestion thất bại
    /// </summary>
    public const int Failed = 3;

    /// <summary>
    /// Đang xóa dữ liệu khỏi Neo4j
    /// </summary>
    public const int Deleting = 4;

    /// <summary>
    /// Dữ liệu Neo4j đã được xóa thành công
    /// </summary>
    public const int Deleted = 5;

    public static string GetStatusName(int? status) => status switch
    {
        Pending    => "Đang chờ",
        Processing => "Đang xử lý",
        Completed  => "Hoàn tất",
        Failed     => "Thất bại",
        Deleting   => "Đang xóa",
        Deleted    => "Đã xóa",
        _          => "Không xác định"
    };
}
