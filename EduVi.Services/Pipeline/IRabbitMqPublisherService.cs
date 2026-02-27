namespace EduVi.Services.Pipeline;

public interface IRabbitMqPublisherService
{
    /// <summary>
    /// Publish một task phân tích bài giảng lên RabbitMQ.
    /// Message format: { taskId, userId, gcsUri, subjectCode, gradeCode, lessonCode }
    /// </summary>
    Task PublishLessonAnalysisTaskAsync(Guid taskId, string userId, string gcsUri, string subjectCode, string gradeCode, string lessonCode);
}
