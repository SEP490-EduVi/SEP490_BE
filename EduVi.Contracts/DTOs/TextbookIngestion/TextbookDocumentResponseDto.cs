using System.Text.Json;

namespace EduVi.Contracts.DTOs.TextbookIngestion;

/// <summary>
/// Response DTO cho TextbookDocument — bao gồm trạng thái và kết quả ingestion
/// </summary>
public class TextbookDocumentResponseDto
{
    public string DocumentCode { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string GradeCode { get; set; } = string.Empty;
    public int? PublishYear { get; set; }
    public string? Publisher { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>
    /// Thống kê ingestion (JSON object). Null nếu chưa hoàn tất.
    /// </summary>
    public JsonElement? Stats { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Cảnh báo nếu đã tồn tại một bản ghi ingestion thành công cho cùng SubjectCode + GradeCode.
    /// Upload vẫn được tiếp tục xử lý bình thường.
    /// </summary>
    public string? Warning { get; set; }
}
