using System.Text.Json;

namespace EduVi.Contracts.DTOs.CurriculumIngestion;

/// <summary>
/// Response DTO cho CurriculumDocument — bao gồm trạng thái và kết quả ingestion
/// </summary>
public class CurriculumDocumentResponseDto
{
    public string DocumentCode { get; set; } = string.Empty;
    public string SubjectCode { get; set; } = string.Empty;
    public string EducationLevel { get; set; } = string.Empty;
    public int CurriculumYear { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>
    /// Thống kê ingestion (JSON object). e.g. {"chu_de_count":57, "yeu_cau_count":180}
    /// Null nếu chưa hoàn tất.
    /// </summary>
    public JsonElement? Stats { get; set; }

    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Cảnh báo nếu đã tồn tại một bản ghi đã ingestion thành công cho cùng SubjectCode + EducationLevel + CurriculumYear.
    /// Upload vẫn được tiếp tục xử lý bình thường.
    /// </summary>
    public string? Warning { get; set; }
}
