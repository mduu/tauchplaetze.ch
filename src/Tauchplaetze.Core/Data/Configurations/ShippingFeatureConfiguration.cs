using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Shipping;

namespace Tauchplaetze.Core.Data.Configurations;

internal class ShippingFeatureConfiguration : IEntityTypeConfiguration<ShippingFeature>
{
    public void Configure(EntityTypeBuilder<ShippingFeature> builder)
    {
        builder.ToTable("ShippingFeatures");
        builder.Property(f => f.Name).HasMaxLength(ShippingRules.NameLength);
        builder.Property(f => f.Geometry).HasColumnType("geography");
        builder.Property(f => f.SourceKey).HasMaxLength(ShippingRules.SourceKeyLength).IsUnicode(false);
        builder.HasIndex(f => new { f.Source, f.SourceKey }).IsUnique();
    }
}
