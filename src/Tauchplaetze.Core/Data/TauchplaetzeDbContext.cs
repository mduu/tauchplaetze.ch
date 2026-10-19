using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Data;

internal class TauchplaetzeDbContext(DbContextOptions<TauchplaetzeDbContext> options)
    : IdentityDbContext<User, IdentityRole, string>(options)
{
    public DbSet<WaterBody> WaterBodies => Set<WaterBody>();

    public DbSet<DiveSite> DiveSites => Set<DiveSite>();

    public DbSet<DiveSiteTranslation> DiveSiteTranslations => Set<DiveSiteTranslation>();

    public DbSet<DiveSiteEntryPoint> DiveSiteEntryPoints => Set<DiveSiteEntryPoint>();

    public DbSet<DiveSiteNotice> DiveSiteNotices => Set<DiveSiteNotice>();

    public DbSet<DiveSiteRevision> DiveSiteRevisions => Set<DiveSiteRevision>();

    public DbSet<SlugRedirect> SlugRedirects => Set<SlugRedirect>();

    public DbSet<DailyForecast> DailyForecasts => Set<DailyForecast>();

    public DbSet<ForecastRun> ForecastRuns => Set<ForecastRun>();

    public DbSet<SiteWeatherOutlook> SiteWeatherOutlooks => Set<SiteWeatherOutlook>();

    public DbSet<MediaItem> MediaItems => Set<MediaItem>();

    public DbSet<WaterBodyHydroStation> WaterBodyHydroStations => Set<WaterBodyHydroStation>();

    public DbSet<HydroHour> HydroHours => Set<HydroHour>();

    public DbSet<HydroReading> HydroReadings => Set<HydroReading>();

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
        configurationBuilder.Properties<ProfileSource>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<VisibilityIndicator>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ClaritySource>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<AlgaeLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<CurrentLevel>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<ForecastRunStatus>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<HydroStationRole>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<EntryPointKind>().HaveConversion<string>().HaveMaxLength(20);
        configurationBuilder.Properties<NoticeKind>().HaveConversion<string>().HaveMaxLength(20);
    }
}
