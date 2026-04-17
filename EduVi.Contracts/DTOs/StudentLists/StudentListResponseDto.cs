namespace EduVi.Contracts.DTOs.StudentLists;

public class StudentListResponseDto
{
    public int StudentListId { get; set; }
    public string StudentListCode { get; set; } = string.Empty;
    public int TeacherId { get; set; }
    public string Description { get; set; } = string.Empty;

    /// <summary>Danh sách tên học sinh đã import.</summary>
    public List<string> Students { get; set; } = new();

    public int StudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
