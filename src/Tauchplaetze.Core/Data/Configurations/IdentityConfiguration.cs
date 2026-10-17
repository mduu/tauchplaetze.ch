using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Data.Configurations;

internal class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.DisplayName).HasMaxLength(60);
        builder.HasIndex(u => u.DisplayName).IsUnique();
        builder.Property(u => u.PreferredCulture).HasMaxLength(2).IsFixedLength();
        builder.Property(u => u.BlockedBy).HasMaxLength(450);
    }
}

/// <remarks>Die Rollen haben feste Kennungen. So bleibt die Migration bei jedem Lauf gleich.</remarks>
internal class RoleConfiguration : IEntityTypeConfiguration<IdentityRole>
{
    private static readonly Dictionary<string, string> RoleIds = new()
    {
        [Roles.Contributor] = "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a01",
        [Roles.Moderator] = "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a02",
        [Roles.Admin] = "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a03",
    };

    public void Configure(EntityTypeBuilder<IdentityRole> builder) =>
        builder.HasData(Roles.All.Select(role => new IdentityRole
        {
            Id = RoleIds[role],
            Name = role,
            NormalizedName = role.ToUpperInvariant(),
            ConcurrencyStamp = RoleIds[role],
        }));
}
