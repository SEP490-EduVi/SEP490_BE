using EduVi.Contracts.DTOs.CurriculumIngestion;

namespace EduVi.Services.CurriculumIngestion;

public interface ICurriculumIngestionService
{
    /// <summary>
    /// Upload file .docx → GCS → tạo DB record → publish RabbitMQ → trả về 202
    /// </summary>
    Task<CurriculumDocumentResponseDto> UploadCurriculumDocumentAsync(int adminUserId, UploadCurriculumDocumentRequestDto request);

    /// <summary>
    /// Danh sách tất cả curriculum documents + trạng thái (admin dashboard)
    /// </summary>
    Task<List<CurriculumDocumentResponseDto>> GetAllCurriculumDocumentsAsync();

    /// <summary>
    /// Xem chi tiết một curriculum document theo DocumentCode (polling status)
    /// </summary>
    Task<CurriculumDocumentResponseDto> GetCurriculumDocumentByCodeAsync(string documentCode);

    /// <summary>
    /// Xóa dữ liệu curriculum khỏi Neo4j — đặt status Deleting → publish deletion task
    /// </summary>
    Task DeleteCurriculumNeo4jAsync(string documentCode);
}
