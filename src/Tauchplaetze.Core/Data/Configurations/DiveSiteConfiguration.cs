using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DiveSiteConfiguration : IEntityTypeConfiguration<DiveSite>
{
    private static readonly string[] RatingColumns =
    [
        nameof(DiveSite.DifficultyRating),
        nameof(DiveSite.WallRating),
        nameof(DiveSite.MarineLifeRating),
        nameof(DiveSite.WrecksRating),
        nameof(DiveSite.CurrentRating),
        nameof(DiveSite.SurroundingsRating),
    ];

    public void Configure(EntityTypeBuilder<DiveSite> builder)
    {
        builder.ToTable("DiveSites", table =>
        {
            foreach (var column in RatingColumns)
            {
                table.HasCheckConstraint($"CK_DiveSites_{column}", $"[{column}] BETWEEN 1 AND 5");
            }

            table.HasCheckConstraint("CK_DiveSites_DataLevel", "[DataLevel] BETWEEN 0 AND 3");
            table.HasCheckConstraint("CK_DiveSites_ContentLevel", "[ContentLevel] BETWEEN 0 AND 2");
        });

        builder.Property(d => d.Id).ValueGeneratedNever();
        builder.HasIndex(d => d.Slug).IsUnique();
        builder.Property(d => d.Slug).HasMaxLength(120);
        builder.Property(d => d.Name).HasMaxLength(120);
        builder.Property(d => d.Location).HasColumnType("geography");
        builder.Property(d => d.SatellitePoint).HasColumnType("geography");
        builder.Property(d => d.Municipality).HasMaxLength(80);
        builder.Property(d => d.Canton).HasMaxLength(2).IsFixedLength();
        builder.Property(d => d.InflowStationOverride).HasMaxLength(20);
        builder.Property(d => d.CreatedBy).HasMaxLength(450);
        builder.Property(d => d.UpdatedBy).HasMaxLength(450);
        builder.Property(d => d.RowVersion).IsRowVersion();

        builder.HasOne(d => d.WaterBody).WithMany().HasForeignKey(d => d.WaterBodyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Translations).WithOne().HasForeignKey(t => t.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.EntryPoints).WithOne().HasForeignKey(e => e.DiveSiteId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => new { d.WaterBodyId, d.Status });
    }
}
