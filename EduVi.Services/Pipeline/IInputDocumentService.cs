using EduVi.Contracts.DTOs.Pipeline;

namespace EduVi.Services.Pipeline;

public interface IInputDocumentService
{
    /// <summary>
    /// Upload file lên GCS và lưu metadata vào DB (InputDocuments)
    /// </summary>
    Task<InputDocumentResponseDto> UploadInputDocumentAsync(int teacherId, UploadInputDocumentRequestDto request);

    /// <summary>
    /// Lấy danh sách InputDocuments của một Teacher
    /// </summary>
    Task<List<InputDocumentResponseDto>> GetInputDocumentsByTeacherAsync(int teacherId);

    /// <summary>
    /// Lấy danh sách InputDocuments của một Teacher theo ProjectCode
    /// </summary>
    Task<List<InputDocumentResponseDto>> GetInputDocumentsByProjectCodeAsync(int teacherId, string projectCode);

    /// <summary>
    /// Lấy chi tiết InputDocument theo DocumentCode của Teacher hiện tại
    /// </summary>
    Task<InputDocumentResponseDto> GetInputDocumentByCodeAsync(int teacherId, string documentCode);

    /// <summary>
    /// Xóa cứng InputDocument và file trong GCS
    /// </summary>
    Task DeleteInputDocumentAsync(int teacherId, string documentCode);
}
