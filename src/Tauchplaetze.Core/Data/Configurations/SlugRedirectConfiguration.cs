using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class SlugRedirectConfiguration : IEntityTypeConfiguration<SlugRedirect>
{
    public void Configure(EntityTypeBuilder<SlugRedirect> builder)
    {
        builder.ToTable("SlugRedirects");
        builder.HasKey(r => r.OldSlug);
        builder.Property(r => r.OldSlug).HasMaxLength(120);
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(r => r.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
