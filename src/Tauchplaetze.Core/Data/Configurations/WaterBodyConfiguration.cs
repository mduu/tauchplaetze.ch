using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class WaterBodyConfiguration : IEntityTypeConfiguration<WaterBody>
{
    public void Configure(EntityTypeBuilder<WaterBody> builder)
    {
        builder.ToTable("WaterBodies");
        builder.HasIndex(w => w.Slug).IsUnique();
        builder.Property(w => w.Slug).HasMaxLength(80);
        builder.Property(w => w.NameDe).HasMaxLength(100);
        builder.Property(w => w.NameEn).HasMaxLength(100);
        builder.Property(w => w.NameFr).HasMaxLength(100);
        builder.Property(w => w.NameIt).HasMaxLength(100);
        builder.Property(w => w.Outline).HasColumnType("geography");
        builder.Property(w => w.Centroid).HasColumnType("geography");
        builder.Property(w => w.SatellitePoint).HasColumnType("geography");
        builder.Property(w => w.SwissTlmId).HasMaxLength(40);
        builder.Property(w => w.AlplakesLakeKey).HasMaxLength(50);
        builder.Property(w => w.AlplakesModel3d).HasMaxLength(50);
        builder.Property(w => w.AlplakesSimstratKey).HasMaxLength(50);
    }
}
