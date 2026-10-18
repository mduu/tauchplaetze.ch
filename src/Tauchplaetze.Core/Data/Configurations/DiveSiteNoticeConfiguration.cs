using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DiveSiteNoticeConfiguration : IEntityTypeConfiguration<DiveSiteNotice>
{
    public void Configure(EntityTypeBuilder<DiveSiteNotice> builder)
    {
        builder.ToTable("DiveSiteNotices");
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.HasIndex(n => new { n.DiveSiteId, n.SortOrder });
        builder.HasMany(n => n.Translations).WithOne().HasForeignKey(t => t.NoticeId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal class DiveSiteNoticeTranslationConfiguration : IEntityTypeConfiguration<DiveSiteNoticeTranslation>
{
    public void Configure(EntityTypeBuilder<DiveSiteNoticeTranslation> builder)
    {
        builder.ToTable("DiveSiteNoticeTranslations");
        builder.HasKey(t => new { t.NoticeId, t.Culture });
        builder.Property(t => t.Culture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.Text).HasMaxLength(NoticeRules.TextLength);
    }
}
