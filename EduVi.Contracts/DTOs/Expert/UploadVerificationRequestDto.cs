using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;

namespace EduVi.Contracts.DTOs.Expert;

public class UploadVerificationRequestDto
{
    [Required]
    public IFormFile File { get; set; } = null!;

    /// <summary>
    /// Loại tài liệu: degree | certificate | id_card | other
    /// </summary>
    [Required]
    public string FileType { get; set; } = null!;

    public string? Description { get; set; }
}
