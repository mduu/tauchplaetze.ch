using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DiveSiteRevisionConfiguration : IEntityTypeConfiguration<DiveSiteRevision>
{
    public void Configure(EntityTypeBuilder<DiveSiteRevision> builder)
    {
        builder.ToTable("DiveSiteRevisions");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasIndex(r => new { r.DiveSiteId, r.Number }).IsUnique();
        builder.HasIndex(r => new { r.DiveSiteId, r.Status });
        builder.HasIndex(r => new { r.AuthorId, r.Status });
        builder.Property(r => r.SnapshotPath).HasMaxLength(300);
        builder.Property(r => r.SnapshotHash).HasMaxLength(64).IsFixedLength();
        builder.Property(r => r.Comment).HasMaxLength(500);
        builder.Property(r => r.ReviewComment).HasMaxLength(500);
        builder.Property(r => r.AuthorId).HasMaxLength(450);
        builder.Property(r => r.ReviewerId).HasMaxLength(450);
        builder.Property(r => r.RedactedBy).HasMaxLength(450);
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(r => r.DiveSiteId).OnDelete(DeleteBehavior.Restrict);
    }
}
