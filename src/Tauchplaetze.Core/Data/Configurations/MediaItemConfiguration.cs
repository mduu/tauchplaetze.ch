using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class MediaItemConfiguration : IEntityTypeConfiguration<MediaItem>
{
    public void Configure(EntityTypeBuilder<MediaItem> builder)
    {
        builder.ToTable("MediaItems");
        builder.Property(m => m.Id).ValueGeneratedNever();
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(m => m.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WaterBody>().WithMany().HasForeignKey(m => m.WaterBodyId).OnDelete(DeleteBehavior.NoAction);
        builder.HasIndex(m => new { m.DiveSiteId, m.Section, m.SortOrder });
        builder.HasIndex(m => new { m.UploadedBy, m.UploadedAt });
        builder.Property(m => m.Section).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.Kind).HasConversion<string>().HasMaxLength(20);
        builder.Property(m => m.BlobPath).HasMaxLength(300);
        builder.Property(m => m.Photographer).HasMaxLength(120);
        builder.Property(m => m.PhotographerUrl).HasMaxLength(300);
        builder.Property(m => m.SourceUrl).HasMaxLength(300);
        builder.Property(m => m.UploadedBy).HasMaxLength(450);
        builder.HasMany(m => m.Translations).WithOne().HasForeignKey(t => t.MediaItemId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class MediaItemTranslationConfiguration : IEntityTypeConfiguration<MediaItemTranslation>
{
    public void Configure(EntityTypeBuilder<MediaItemTranslation> builder)
    {
        builder.ToTable("MediaItemTranslations");
        builder.HasKey(t => new { t.MediaItemId, t.Culture });
        builder.Property(t => t.Culture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.Caption).HasMaxLength(300);
        builder.Property(t => t.AltText).HasMaxLength(300);
    }
}
