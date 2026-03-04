namespace EduVi.Services.Pipeline;

public interface IRabbitMqPublisherService
{
    /// <summary>
    /// Publish một task phân tích bài giảng lên RabbitMQ.
    /// Message format: { taskId, userId, productId, gcsUri, subjectCode, gradeCode, lessonCode }
    /// </summary>
    Task PublishLessonAnalysisTaskAsync(Guid taskId, string userId, int productId, string gcsUri, string subjectCode, string gradeCode, string lessonCode);

    /// <summary>
    /// Publish một task tạo slide presentation lên RabbitMQ.
    /// Message format: { taskId, userId, productId, evaluationResult, lessonPlanText, textbookSections, preferences }
    /// </summary>
    Task PublishSlideGenerationTaskAsync(Guid taskId, string userId, int productId, object evaluationResult, string lessonPlanText, object textbookSections, string slideRange);
}
