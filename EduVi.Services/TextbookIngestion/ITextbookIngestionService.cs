using EduVi.Contracts.DTOs.TextbookIngestion;

namespace EduVi.Services.TextbookIngestion;

public interface ITextbookIngestionService
{
    /// <summary>
    /// Upload file .pdf → GCS → tạo DB record → publish RabbitMQ → trả về 202
    /// </summary>
    Task<TextbookDocumentResponseDto> UploadTextbookDocumentAsync(int adminUserId, UploadTextbookDocumentRequestDto request);

    /// <summary>
    /// Danh sách tất cả textbook documents + trạng thái (admin dashboard)
    /// </summary>
    Task<List<TextbookDocumentResponseDto>> GetAllTextbookDocumentsAsync();

    /// <summary>
    /// Xem chi tiết một textbook document theo DocumentCode (polling status)
    /// </summary>
    Task<TextbookDocumentResponseDto> GetTextbookDocumentByCodeAsync(string documentCode);

    /// <summary>
    /// Xóa dữ liệu textbook khỏi Neo4j — đặt status Deleting → publish deletion task
    /// </summary>
    Task DeleteTextbookNeo4jAsync(string documentCode);
}
