namespace EduVi.Services.Pipeline;

public interface IRabbitMqPublisherService
{
    /// <summary>
    /// Publish một task phân tích bài giảng lên RabbitMQ.
    /// Message format: { taskId, userId, productId, gcsUri, subjectCode, gradeCode, lessonCode, curriculumYear }
    /// </summary>
    Task PublishLessonAnalysisTaskAsync(Guid taskId, string userId, int productId, string gcsUri, string subjectCode, string gradeCode, string lessonCode, int? curriculumYear);

    /// <summary>
    /// Publish một task tạo slide presentation lên RabbitMQ.
    /// Message format: { taskId, userId, productId, evaluationResult, lessonPlanText, preferences }
    /// </summary>
    Task PublishSlideGenerationTaskAsync(Guid taskId, string userId, int productId, object evaluationResult, string lessonPlanText, string slideRange);

    /// <summary>
    /// Publish một task tạo video từ slide edited document (GCS URL) lên RabbitMQ.
    /// Message format: { taskId, requestId, userId, productId, productCode, slideEditedDocumentUrl }
    /// </summary>
    Task PublishVideoGenerationTaskAsync(Guid taskId, string productVideoCode, string userId, int productId, string productCode, string slideEditedDocumentUrl);

    /// <summary>
    /// Publish một task curriculum ingestion lên RabbitMQ.
    /// Message format: { taskId, documentId, gcsUri, subjectCode, educationLevel, curriculumYear }
    /// </summary>
    Task PublishCurriculumIngestionTaskAsync(Guid taskId, int documentId, string gcsUri, string subjectCode, string educationLevel, int curriculumYear);

    /// <summary>
    /// Publish một task xóa curriculum khỏi Neo4j lên RabbitMQ.
    /// Message format: { taskId, documentId, subjectCode, educationLevel, curriculumYear }
    /// </summary>
    Task PublishCurriculumDeletionTaskAsync(Guid taskId, int documentId, string subjectCode, string educationLevel, int? curriculumYear);

    /// <summary>
    /// Publish một task textbook ingestion lên RabbitMQ.
    /// Message format: { taskId, documentId, gcsUri, subjectCode, gradeCode }
    /// </summary>
    Task PublishTextbookIngestionTaskAsync(Guid taskId, int documentId, string gcsUri, string subjectCode, string gradeCode);

    /// <summary>
    /// Publish một task xóa textbook khỏi Neo4j lên RabbitMQ.
    /// Message format: { taskId, documentId, subjectCode, gradeCode }
    /// </summary>
    Task PublishTextbookDeletionTaskAsync(Guid taskId, int documentId, string subjectCode, string gradeCode);

    /// <summary>
    /// Publish một task tạo game lên RabbitMQ.
    /// </summary>
    Task PublishGameGenerationTaskAsync(Guid taskId, object message);
}
