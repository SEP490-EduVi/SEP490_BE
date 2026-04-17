using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduVi.Contracts.DTOs.StudentLists;

public class CreateStudentListRequest
{
    [JsonPropertyName("description")]
    [Required]
    public string Description { get; set; } = string.Empty;
}

public class UpdateStudentListRequest
{
    [JsonPropertyName("description")]
    public string? Description { get; set; }
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
