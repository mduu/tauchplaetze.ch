using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class MachineTranslationConfiguration : IEntityTypeConfiguration<MachineTranslation>
{
    public void Configure(EntityTypeBuilder<MachineTranslation> builder)
    {
        builder.ToTable("MachineTranslations");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.RequestedBy).HasMaxLength(450);
        builder.Property(t => t.SourceCulture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.TargetCulture).HasMaxLength(2).IsFixedLength();
        builder.Property(t => t.SourceHash).HasMaxLength(64).IsFixedLength();
        builder.Property(t => t.ResultHash).HasMaxLength(64).IsFixedLength();
        builder.Property(t => t.Model).HasMaxLength(60);
        builder.HasIndex(t => t.SourceHash);
        builder.HasIndex(t => t.ResultHash);
        builder.HasIndex(t => new { t.RequestedBy, t.CreatedAt });
        builder.HasIndex(t => t.CreatedAt);
    }
}
