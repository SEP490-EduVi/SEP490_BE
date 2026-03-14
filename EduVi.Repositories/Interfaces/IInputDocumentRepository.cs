using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IInputDocumentRepository
{
    Task<InputDocuments?> GetInputDocumentByIdAsync(int documentId);

    Task<InputDocuments?> GetInputDocumentByCodeAsync(string documentCode);

    Task<InputDocuments?> GetInputDocumentByCodeAndTeacherAsync(string documentCode, int teacherId);

    Task<InputDocuments?> GetExistingInputDocumentAsync(int teacherId, int projectId, int subjectId, int gradeId, int? lessonId);

    Task<InputDocuments> CreateInputDocumentAsync(InputDocuments document);

    void UpdateInputDocument(InputDocuments document);

    void DeleteInputDocument(InputDocuments document);

    Task<List<InputDocuments>> GetInputDocumentsByTeacherAsync(int teacherId);

    Task<List<InputDocuments>> GetInputDocumentsByTeacherAndProjectAsync(int teacherId, int projectId);
}
