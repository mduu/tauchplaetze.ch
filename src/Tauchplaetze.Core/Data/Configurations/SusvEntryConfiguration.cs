using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Susv;

namespace Tauchplaetze.Core.Data.Configurations;

internal class SusvEntryConfiguration : IEntityTypeConfiguration<SusvEntry>
{
    public void Configure(EntityTypeBuilder<SusvEntry> builder)
    {
        builder.ToTable("SusvEntries");
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.HasIndex(e => new { e.SourceId, e.Topic }).IsUnique();
        builder.HasIndex(e => e.ReviewStatus);
        builder.Property(e => e.ContentHash).HasMaxLength(64).IsFixedLength().IsUnicode(false);
        builder.Property(e => e.ReviewedBy).HasMaxLength(450);
        builder.HasMany(e => e.Translations).WithOne().HasForeignKey(t => t.EntryId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(e => e.Sites).WithOne().HasForeignKey(s => s.EntryId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class SusvEntryTranslationConfiguration : IEntityTypeConfiguration<SusvEntryTranslation>
{
    public void Configure(EntityTypeBuilder<SusvEntryTranslation> builder)
    {
        builder.ToTable("SusvEntryTranslations");
        builder.HasKey(t => new { t.EntryId, t.Culture });
        builder.Property(t => t.Culture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.Title).HasMaxLength(SusvRules.TitleLength);
        builder.Property(t => t.Teaser).HasMaxLength(SusvRules.TeaserLength);
        builder.Property(t => t.Text).HasMaxLength(SusvRules.TextLength);
        builder.Property(t => t.DetailUrl).HasMaxLength(SusvRules.UrlLength);
        builder.Property(t => t.ReviewedTitle).HasMaxLength(SusvRules.TitleLength);
        builder.Property(t => t.ReviewedText).HasMaxLength(SusvRules.TextLength);
    }
}

internal class SusvEntrySiteConfiguration : IEntityTypeConfiguration<SusvEntrySite>
{
    public void Configure(EntityTypeBuilder<SusvEntrySite> builder)
    {
        builder.ToTable("SusvEntrySites");
        builder.HasKey(s => new { s.EntryId, s.DiveSiteId });
        builder.HasIndex(s => s.DiveSiteId);
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(s => s.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
