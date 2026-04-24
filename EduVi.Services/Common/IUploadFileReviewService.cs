namespace EduVi.Services.Common;

public interface IUploadFileReviewService
{
    Task PublishVerificationReviewTaskAsync(
        Guid taskId,
        int expertId,
        string verificationCode,
        string fileUrl,
        string fileName,
        string contentType,
        string fileType,
        string? description);

    Task PublishMaterialReviewTaskAsync(
        Guid taskId,
        int expertId,
        string materialCode,
        string fileUrl,
        string fileName,
        string contentType,
        string fileType,
        string? title,
        string? description,
        string? previewUrl,
        string? subjectCode,
        string? gradeCode);
}
