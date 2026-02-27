namespace EduVi.Contracts.DTOs.Pipeline;

/// <summary>
/// Response trả về ngay khi task được queue thành công
/// </summary>
public class PipelineTaskResponseDto
{
    public Guid TaskId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
