using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DiveSiteTranslationConfiguration : IEntityTypeConfiguration<DiveSiteTranslation>
{
    public void Configure(EntityTypeBuilder<DiveSiteTranslation> builder)
    {
        builder.ToTable("DiveSiteTranslations");
        builder.HasKey(t => new { t.DiveSiteId, t.Culture });
        builder.Property(t => t.Culture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.SurroundingsInfo).HasMaxLength(2000);
        builder.Property(t => t.ApproachInfo).HasMaxLength(2000);
        builder.Property(t => t.HazardsInfo).HasMaxLength(2000);
        builder.Property(t => t.FacilitiesInfo).HasMaxLength(1000);
        builder.Property(t => t.DivingBanNote).HasMaxLength(500);
    }
}
