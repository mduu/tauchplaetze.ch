using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Data;

internal class TauchplaetzeDbContext(DbContextOptions<TauchplaetzeDbContext> options)
    : IdentityDbContext<User, IdentityRole, string>(options)
{
    public DbSet<WaterBody> WaterBodies => Set<WaterBody>();

    public DbSet<DiveSite> DiveSites => Set<DiveSite>();

    public DbSet<DiveSiteTranslation> DiveSiteTranslations => Set<DiveSiteTranslation>();

    public DbSet<DiveSiteRevision> DiveSiteRevisions => Set<DiveSiteRevision>();

    public DbSet<SlugRedirect> SlugRedirects => Set<SlugRedirect>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(TauchplaetzeDbContext).Assembly);
    }

    /// <remarks>Enums stehen als Text in der Datenbank. So bleibt eine Row lesbar, auch wenn sich die Reihenfolge der Werte ändert (`datastructure.md`).</remarks>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<WaterBodyType>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DiveSiteStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<DiveSiteAccess>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<RevisionStatus>().HaveConversion<string>().HaveMaxLength(20);
    }
}
