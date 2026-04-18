#nullable disable
using System;

namespace EduVi.Repositories.Models;

public partial class ProductGames
{
    public int ProductGameId { get; set; }

    public int ProductId { get; set; }

    public string ProductGameCode { get; set; }

    public Guid TaskId { get; set; }

    public string ProductGameName { get; set; }

    public string TemplateCode { get; set; }

    public int RoundCount { get; set; }

    /// <summary>0 = Queued, 1 = Completed, 2 = Failed, 3 = Deleted — xem GameStatusConstants</summary>
    public int Status { get; set; }

    public string ResultJson { get; set; }

    public string ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual Products Product { get; set; }
}
