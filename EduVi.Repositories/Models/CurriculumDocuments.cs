#nullable disable
using System;

namespace EduVi.Repositories.Models;

public partial class CurriculumDocuments
{
    public int CurriculumDocumentId { get; set; }

    public string DocumentCode { get; set; }

    public string SubjectCode { get; set; }

    public string EducationLevel { get; set; }

    public int CurriculumYear { get; set; }

    public string OriginalFileName { get; set; }

    public string FileUrl { get; set; }

    /// <summary>0 = Pending, 1 = Processing, 2 = Completed, 3 = Failed — xem CurriculumDocumentStatusConstants</summary>
    public int Status { get; set; }

    public string Note { get; set; }

    public string Stats { get; set; }

    public string ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }
}
