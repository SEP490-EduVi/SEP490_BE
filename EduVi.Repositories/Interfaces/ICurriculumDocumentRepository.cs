using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface ICurriculumDocumentRepository
{
    Task<CurriculumDocuments?> GetByIdAsync(int curriculumDocumentId);

    Task<CurriculumDocuments?> GetByDocumentCodeAsync(string documentCode);

    Task<List<CurriculumDocuments>> GetAllAsync();

    Task<CurriculumDocuments> CreateAsync(CurriculumDocuments document);

    void Update(CurriculumDocuments document);

    Task<bool> ExistsCompletedAsync(string subjectCode, string educationLevel, int curriculumYear);
}
