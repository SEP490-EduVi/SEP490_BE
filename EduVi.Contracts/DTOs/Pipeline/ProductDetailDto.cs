namespace EduVi.Contracts.DTOs.Pipeline;

using System.Text.Json;

/// <summary>
/// Chi tiết đầy đủ của Product — bao gồm dữ liệu của tất cả các bước pipeline
/// </summary>
public class ProductDetailDto
{
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Status { get; set; }
    public string StatusName { get; set; } = string.Empty;

    // ── Bước 1: Lesson Analysis ──────────────────────────────────────────
    /// <summary>Kết quả đánh giá AI (JSON object). Null nếu chưa evaluate.</summary>
    public JsonElement? EvaluationResult { get; set; }
    public DateTime? EvaluatedAt { get; set; }

    /// <summary>Lesson plan text được rút ra từ tài liệu (plain text).</summary>
    public string? LessonPlanText { get; set; }

    // ── Bước 2: Slide Generation
    /// <summary>Slide JSON do AI tạo ra (JSON object). Null nếu chưa generate.</summary>
    public JsonElement? SlideDocument { get; set; }
    public DateTime? SlideGeneratedAt { get; set; }

    // ── Bước 3: Teacher Edit ─────────────────────────────────────────────
    /// <summary>Slide JSON sau khi Teacher chỉnh sửa (JSON object). Null nếu chưa edit.</summary>
    public JsonElement? SlideEditedDocument { get; set; }
    public DateTime? SlideEditedAt { get; set; }

    // ── Bước 4: Video Generation ─────────────────────────────────────────
    /// <summary>Video URL sau khi AI tạo xong.</summary>
    public string? VideoUrl { get; set; }
    public double? VideoDuration { get; set; }
    public string? ProductVideoCode { get; set; }
    public JsonElement? VideoInteractions { get; set; }
    public JsonElement? VideoPausePoints { get; set; }
    public DateTime? VideoGeneratedAt { get; set; }
}
