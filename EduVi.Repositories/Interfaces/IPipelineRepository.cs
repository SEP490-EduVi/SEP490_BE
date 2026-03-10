using EduVi.Repositories.Models;

namespace EduVi.Repositories.Interfaces;

public interface IPipelineRepository
{
    // ============ InputDocuments ============

    /// <summary>
    /// Lấy InputDocument theo ID (kèm Subject, Grade, Lesson info)
    /// </summary>
    Task<InputDocuments?> GetInputDocumentByIdAsync(int documentId);

    /// <summary>
    /// Lấy InputDocument theo Code (kèm Subject, Grade, Lesson info)
    /// </summary>
    Task<InputDocuments?> GetInputDocumentByCodeAsync(string documentCode);

    /// <summary>
    /// Tìm InputDocument đã tồn tại theo Teacher + Subject + Grade + Lesson
    /// </summary>
    Task<InputDocuments?> GetExistingInputDocumentAsync(int teacherId, int subjectId, int gradeId, int? lessonId);

    /// <summary>
    /// Tạo InputDocument mới
    /// </summary>
    Task<InputDocuments> CreateInputDocumentAsync(InputDocuments document);

    /// <summary>
    /// Cập nhật InputDocument đã tồn tại
    /// </summary>
    void UpdateInputDocument(InputDocuments document);

    /// <summary>
    /// Lấy danh sách InputDocuments của một Teacher
    /// </summary>
    Task<List<InputDocuments>> GetInputDocumentsByTeacherAsync(int teacherId);

    // ============ Projects ============

    /// <summary>
    /// Lấy tất cả Projects của một Teacher (kèm InputDocument)
    /// </summary>
    Task<List<Projects>> GetProjectsByTeacherAsync(int teacherId);

    /// <summary>
    /// Lấy Project theo Code (kèm InputDocument)
    /// </summary>
    Task<Projects?> GetProjectByCodeAsync(string projectCode, bool includeRelations = false);

    /// <summary>
    /// Kiểm tra Project có thuộc về Teacher không (by Code)
    /// </summary>
    Task<Projects?> GetProjectByCodeAndTeacherAsync(string projectCode, int teacherId);

    /// <summary>
    /// Tạo Project mới
    /// </summary>
    Task<Projects> CreateProjectAsync(Projects project);

    /// <summary>
    /// Cập nhật Project
    /// </summary>
    void UpdateProject(Projects project);

    /// <summary>
    /// Xóa Project
    /// </summary>
    void DeleteProject(Projects project);

    // ============ Products ============

    /// <summary>
    /// Tìm Product đã tồn tại theo ProjectId + SourceInputId
    /// </summary>
    Task<Products?> GetExistingProductAsync(int projectId, int sourceInputId);

    /// <summary>
    /// Lấy danh sách Products của một Teacher (không bao gồm Deleted)
    /// </summary>
    Task<List<Products>> GetProductsByTeacherAsync(int teacherId);

    /// <summary>
    /// Tạo Product mới với status NEW
    /// </summary>
    Task<Products> CreateProductAsync(Products product);

    /// <summary>
    /// Lấy Product theo ProductId
    /// </summary>
    Task<Products?> GetProductByIdAsync(int productId);

    /// <summary>
    /// Lấy Product theo ProductCode + TeacherId
    /// </summary>
    Task<Products?> GetProductByCodeAndTeacherAsync(string productCode, int teacherId);

    /// <summary>
    /// Cập nhật Product (status, evaluation result)
    /// </summary>
    void UpdateProduct(Products product);

    /// <summary>
    /// Xóa Product
    /// </summary>
    void DeleteProduct(Products product);

    // ============ ProductComponents ============

    /// <summary>
    /// Lấy danh sách components hiện tại của một Product
    /// </summary>
    Task<List<ProductComponent>> GetProductComponentsAsync(int productId);

    /// <summary>
    /// Xóa toàn bộ components của một Product (để upsert lại)
    /// </summary>
    void DeleteProductComponents(List<ProductComponent> components);

    /// <summary>
    /// Thêm danh sách ProductComponent mới
    /// </summary>
    Task AddProductComponentsAsync(List<ProductComponent> components);

    // ============ Materials ============

    /// <summary>
    /// Lấy MaterialId theo MaterialCode (để map từ Code → ID nội bộ)
    /// </summary>
    Task<int?> GetMaterialIdByCodeAsync(string materialCode);

    /// <summary>
    /// Kiểm tra Teacher đã mua Material này chưa
    /// </summary>
    Task<bool> IsTeacherOwnsMaterialAsync(int teacherId, int materialId);
}
