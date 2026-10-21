using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Reports;

namespace Tauchplaetze.Core.Data.Configurations;

internal class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> builder)
    {
        builder.ToTable("Reports");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(r => r.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(r => new { r.DiveSiteId, r.AuthorId, r.Date }).IsUnique().HasFilter("[AuthorId] IS NOT NULL");
        builder.HasIndex(r => new { r.DiveSiteId, r.Date });
        builder.HasIndex(r => new { r.AuthorId, r.CreatedAt });
        builder.HasIndex(r => r.CommentStatus);
        builder.Property(r => r.Visibility).HasPrecision(3, 1);
        builder.Property(r => r.Temperature).HasPrecision(3, 1);
        builder.Property(r => r.ForecastIndex).HasPrecision(3, 1);
        builder.Property(r => r.Comment).HasMaxLength(ReportRules.CommentMaxLength);
        builder.Property(r => r.CommentStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.AuthorId).HasMaxLength(450);
        builder.Property(r => r.HiddenBy).HasMaxLength(450);
        builder.Property(r => r.ForecastModelVersion).HasMaxLength(20);
    }
}
