using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DiveSiteEntryPointConfiguration : IEntityTypeConfiguration<DiveSiteEntryPoint>
{
    public void Configure(EntityTypeBuilder<DiveSiteEntryPoint> builder)
    {
        builder.ToTable("DiveSiteEntryPoints");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.Location).HasColumnType("geography");
        builder.HasIndex(e => new { e.DiveSiteId, e.SortOrder });
        builder.HasMany(e => e.Translations).WithOne().HasForeignKey(t => t.EntryPointId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class DiveSiteEntryPointTranslationConfiguration : IEntityTypeConfiguration<DiveSiteEntryPointTranslation>
{
    public void Configure(EntityTypeBuilder<DiveSiteEntryPointTranslation> builder)
    {
        builder.ToTable("DiveSiteEntryPointTranslations");
        builder.HasKey(t => new { t.EntryPointId, t.Culture });
        builder.Property(t => t.Culture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.Name).HasMaxLength(80);
        builder.Property(t => t.Description).HasMaxLength(1000);
    }
}
