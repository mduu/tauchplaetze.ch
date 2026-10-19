using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Commands;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

/// <remarks>Die Uhr steht auf dem 17.10.2026. Die Tagesprognosen entstehen direkt in der Datenbank.</remarks>
public class IndexHistoryTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [SqlFact]
    public async Task Der_Verlauf_zeigt_nur_Tage_bis_heute_und_glaettet_ueber_sieben_Kalendertage_auch_ueber_Luecken()
    {
        // Arrange
        var siteId = Guid.CreateVersion7();
        await Sql.Arrange(db =>
        {
            var site = AddSite(db, Lake(), siteId);
            AddForecast(db, site, new DateOnly(2025, 10, 10), 5.0m, "rules-1.0");
            AddForecast(db, site, Today.AddDays(-7), 6.0m, "rules-1.2");
            AddForecast(db, site, Today.AddDays(-5), 9.0m, "rules-1.2");
            AddForecast(db, site, Today.AddDays(-4), null, "rules-1.3");
            AddForecast(db, site, Today, 7.0m, "rules-1.3");
            AddForecast(db, site, Today.AddDays(1), 9.9m, "rules-1.3");
            return Task.CompletedTask;
        });

        // Act
        var history = await Query<GetSiteIndexHistoryQuery, SiteIndexHistory>(new GetSiteIndexHistoryQuery(siteId));

        // Assert
        Assert.Equal([Today.AddDays(-7), Today.AddDays(-5), Today.AddDays(-4), Today], history.Days.Select(d => d.Date));
        Assert.Equal([7.5m, 7.5m, 7.5m, 7.0m], history.Days.Select(d => d.SmoothedIndex));
        Assert.Null(history.Days[2].DiveIndex);
        Assert.Equal([new RuleChange(Today.AddDays(-4), "rules-1.3")], history.RuleChanges);
    }

    [SqlFact]
    public async Task Die_Neuberechnung_wendet_die_aktuelle_Regel_auf_alte_Tage_an_und_baut_den_Median_pro_Gewaesser_auf()
    {
        // Arrange: rules-1.2 gab 7.6 bei mässiger Sicht (5.4 m in Ufernähe). rules-1.3 begrenzt auf 7.0.
        var admin = await Admin();
        var lakeSiteId = Guid.CreateVersion7();
        var secondSiteId = Guid.CreateVersion7();
        await Sql.Arrange(db =>
        {
            var lake = Lake();
            var site = AddSite(db, lake, lakeSiteId);
            var second = AddSite(db, lake, secondSiteId);
            db.DailyForecasts.Add(ModerateVisibilityDay(site, Today.AddDays(-1)));
            AddForecast(db, second, Today.AddDays(-1), 6.0m, "rules-1.3");
            AddForecast(db, second, Today, 5.0m, "rules-1.3");
            AddForecast(db, second, Today.AddDays(2), 9.0m, "rules-1.3");
            return Task.CompletedTask;
        });

        // Act
        var first = await Command<RecomputeForecastHistoryCommand, RecomputeForecastHistoryResult>(new RecomputeForecastHistoryCommand(admin));
        var second = await Command<RecomputeForecastHistoryCommand, RecomputeForecastHistoryResult>(new RecomputeForecastHistoryCommand(admin));

        // Assert
        var yesterday = (await Query<GetSiteIndexHistoryQuery, SiteIndexHistory>(new GetSiteIndexHistoryQuery(lakeSiteId))).Days.Single();
        Assert.Equal((7.0m, "rules-1.3"), (yesterday.DiveIndex, yesterday.ModelVersion));
        Assert.Equal((1, 1, 2), (first.Recomputed, first.Changed, first.Days));
        Assert.Equal((0, 0), (second.Recomputed, second.Changed));

        var lakeId = await LakeId();
        var points = (await Query<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory>(new GetWaterBodyIndexHistoryQuery(lakeId))).Years.Single().Points;
        Assert.Equal([(Today.AddDays(-1), 6.5m, 2), (Today, 5.0m, 1)], points.Select(p => (p.Date, p.MedianIndex, p.Sites)));
    }

    [SqlFact]
    public async Task Nur_Admins_rechnen_die_Vergangenheit_neu()
    {
        // Arrange
        await Admin();
        var anna = await RegisterConfirmed("anna@example.org", "Anna");

        // Act
        var result = await Command<RecomputeForecastHistoryCommand, RecomputeForecastHistoryResult>(new RecomputeForecastHistoryCommand(anna));

        // Assert
        Assert.Equal(AccountError.NotAllowed, result.Error);
    }

    [SqlFact]
    public async Task Jahr_ueber_Jahr_teilt_den_Median_nach_Jahren_und_glaettet_ueber_den_Jahreswechsel()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            db.WaterBodies.Add(Lake());
            return Task.CompletedTask;
        });
        var lakeId = await LakeId();
        await Sql.Arrange(db =>
        {
            db.WaterBodyDailyIndexes.Add(new WaterBodyDailyIndex { WaterBodyId = lakeId, Date = new DateOnly(2025, 12, 30), MedianIndex = 6.0m, Sites = 3, ModelVersion = "rules-1.2" });
            db.WaterBodyDailyIndexes.Add(new WaterBodyDailyIndex { WaterBodyId = lakeId, Date = new DateOnly(2026, 1, 2), MedianIndex = 8.0m, Sites = 4, ModelVersion = "rules-1.3" });
            return Task.CompletedTask;
        });

        // Act
        var history = await Query<GetWaterBodyIndexHistoryQuery, WaterBodyIndexHistory>(new GetWaterBodyIndexHistoryQuery(lakeId));

        // Assert
        Assert.Equal([2025, 2026], history.Years.Select(y => y.Year));
        Assert.Equal([7.0m, 7.0m], history.Years.SelectMany(y => y.Points).Select(p => p.SmoothedIndex));
        Assert.Equal([new RuleChange(new DateOnly(2026, 1, 2), "rules-1.3")], history.RuleChanges);
    }

    private async Task<string> Admin() =>
        (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];

    private async Task<int> LakeId()
    {
        var id = 0;
        await Sql.Arrange(db =>
        {
            id = db.WaterBodies.Single(w => w.Slug == "see").Id;
            return Task.CompletedTask;
        });
        return id;
    }

    private static WaterBody Lake() =>
        new() { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47.28, 8.6), IsActive = true };

    private static DiveSite AddSite(TauchplaetzeDbContext db, WaterBody lake, Guid id)
    {
        var site = new DiveSite
        {
            Id = id,
            Slug = id.ToString("N"),
            Name = "Platz",
            WaterBody = lake,
            Location = GeoPoints.Create(47.2849, 8.6085),
            Status = DiveSiteStatus.Published,
        };
        db.DiveSites.Add(site);
        return site;
    }

    private static void AddForecast(TauchplaetzeDbContext db, DiveSite site, DateOnly date, decimal? index, string modelVersion) =>
        db.DailyForecasts.Add(new DailyForecast
        {
            DiveSiteId = site.Id,
            Date = date,
            DataLevel = 3,
            DiveIndex = index,
            ModelVersion = modelVersion,
            ReasonCodes = "",
        });

    /// <summary>Ein ruhiger Tag mit Profil am Platz und frischem Wert in Ufernähe von 5.4 m, wie Riff Williams am 16.10.2026.</summary>
    private static DailyForecast ModerateVisibilityDay(DiveSite site, DateOnly date) => new()
    {
        DiveSiteId = site.Id,
        Date = date,
        DataLevel = 3,
        DiveIndex = 7.6m,
        ProfileSource = ProfileSource.Model3d,
        SurfaceTemperature = 17.8m,
        TemperatureAt10m = 17.8m,
        TemperatureAt20m = 9.5m,
        TemperatureAt30m = 7.5m,
        ThermoclineTop = 13m,
        ThermoclineBottom = 15m,
        SecchiDepth = 5.4m,
        SecchiDepthShore = 5.4m,
        SecchiObservedAt = date.AddDays(-1),
        ClaritySource = ClaritySource.Sentinel2Shore,
        VisibilityIndicator = VisibilityIndicator.Moderate,
        WindBeaufort = 1,
        WindSpeedKmh = 5m,
        WindGustsKmh = 12m,
        PrecipitationLast72h = 0m,
        AlgaeLevel = AlgaeLevel.Low,
        ModelVersion = "rules-1.2",
        ReasonCodes = "ClearBelowThermocline,CalmLake",
    };
}
