namespace EduVi.Contracts.DTOs.Classroom;

public class ClassroomResponseDto
{
    public string ClassroomCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? GradeLabel { get; set; }
    public string? SchoolYear { get; set; }

    /// <summary>Danh sách tên học sinh đã import.</summary>
    public List<string> Students { get; set; } = new();

    public int StudentCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
