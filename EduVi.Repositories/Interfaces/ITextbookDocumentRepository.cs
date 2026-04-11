using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface ITextbookDocumentRepository
{
    Task<TextbookDocuments?> GetByIdAsync(int textbookDocumentId);

    Task<TextbookDocuments?> GetByDocumentCodeAsync(string documentCode);

    Task<List<TextbookDocuments>> GetAllAsync();

    Task<TextbookDocuments> CreateAsync(TextbookDocuments document);

    void Update(TextbookDocuments document);

    Task<bool> ExistsCompletedAsync(string subjectCode, string gradeCode);
}
