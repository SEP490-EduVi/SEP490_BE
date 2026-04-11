#nullable disable
using System;

namespace EduVi.Repositories.Models;

public partial class TextbookDocuments
{
    public int TextbookDocumentId { get; set; }

    public string DocumentCode { get; set; }

    public string SubjectCode { get; set; }

    public string GradeCode { get; set; }

    public int? PublishYear { get; set; }

    public string Publisher { get; set; }

    public string OriginalFileName { get; set; }

    public string FileUrl { get; set; }

    /// <summary>0 = Pending, 1 = Processing, 2 = Completed, 3 = Failed, 4 = Deleting, 5 = Deleted — xem TextbookDocumentStatusConstants</summary>
    public int Status { get; set; }

    public string Note { get; set; }

    public string Stats { get; set; }

    public string ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public int CreatedBy { get; set; }
}
