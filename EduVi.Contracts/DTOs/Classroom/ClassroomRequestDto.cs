using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.Classroom;

public class CreateClassroomRequest
{
    [JsonPropertyName("name")]
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>Tên lớp hiển thị, ví dụ "10A1", "11B2"</summary>
    [JsonPropertyName("gradeLabel")]
    public string? GradeLabel { get; set; }

    /// <summary>Năm học, ví dụ "2024-2025"</summary>
    [JsonPropertyName("schoolYear")]
    public string? SchoolYear { get; set; }
}

public class UpdateClassroomRequest
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("gradeLabel")]
    public string? GradeLabel { get; set; }

    [JsonPropertyName("schoolYear")]
    public string? SchoolYear { get; set; }
}

public class ImportStudentsRequest
{
    /// <summary>
    /// Danh sách tên học sinh do FE xử lý từ file Excel.
    /// VD: ["Nguyễn Văn A", "Trần Thị B", "Lê Văn C"]
    /// </summary>
    [JsonPropertyName("students")]
    [Required]
    public List<string> Students { get; set; } = new();
}
