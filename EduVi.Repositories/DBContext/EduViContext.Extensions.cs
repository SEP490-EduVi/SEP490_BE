using EduVi.Repositories.Models;
using Microsoft.EntityFrameworkCore;

namespace EduVi.Repositories.DBContext;

public partial class EduViContext
{
    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        // SourceInputId on Products maps to InputDocuments.DocumentId (non-conventional name).
        // EF Core Power Tools did not generate this relationship, so we configure it here.
        modelBuilder.Entity<Products>()
            .HasOne(product => product.SourceInput)
            .WithMany()
            .HasForeignKey(product => product.SourceInputId)
            .HasConstraintName("FK__Products__Source__SourceInputID")
            .OnDelete(DeleteBehavior.ClientSetNull);
    }
}
