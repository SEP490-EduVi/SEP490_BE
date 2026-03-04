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
}
