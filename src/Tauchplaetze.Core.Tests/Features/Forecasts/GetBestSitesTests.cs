using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

/// <remarks>Die Prognosen entstehen direkt in der Datenbank. Der Job rechnet in den Tests nur einen Platz (Testdaten von Riff Williams).</remarks>
public class GetBestSitesTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [SqlFact]
    public async Task Ohne_Ort_gilt_die_ganze_Schweiz_bester_Index_zuerst_und_nur_Plaetze_mit_Index()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        Assert.Equal(["riff-williams", "genf-plage"], best.Ranking.Select(r => r.Slug));
        Assert.Equal(Today, best.Date);
        Assert.Equal(5, best.Days.Count);
    }

    [SqlFact]
    public async Task Mit_Ort_und_Radius_zeigt_die_Rangliste_nur_Plaetze_im_Umkreis_mit_Distanz()
    {
        // Arrange
        await Sql.Arrange(Seed);
        var zurich = new Area(47.37, 8.54, 60);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de", Area: zurich));

        // Assert
        var site = Assert.Single(best.Ranking);
        Assert.Equal("riff-williams", site.Slug);
        Assert.InRange(site.DistanceKm!.Value, 8, 15);
    }

    [SqlFact]
    public async Task Ein_Tag_ausserhalb_der_Prognose_zeigt_heute()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de", Today.AddDays(9)));

        // Assert
        Assert.Equal(Today, best.Date);
    }

    private static Task Seed(TauchplaetzeDbContext db)
    {
        var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
        Add(db, lake, "riff-williams", 47.2849, 8.6085, 8.4m);
        Add(db, lake, "genf-plage", 46.21, 6.15, 6.1m);
        Add(db, lake, "nur-wetter", 47.30, 8.60, null);
        return Task.CompletedTask;
    }

    private static void Add(TauchplaetzeDbContext db, WaterBody lake, string slug, double latitude, double longitude, decimal? index)
    {
        var site = new DiveSite
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            Name = slug,
            WaterBody = lake,
            Location = GeoPoints.Create(latitude, longitude),
            Status = DiveSiteStatus.Published,
        };
        db.DiveSites.Add(site);
        db.DailyForecasts.Add(new DailyForecast
        {
            DiveSiteId = site.Id,
            Date = Today,
            DataLevel = index is null ? (byte)1 : (byte)3,
            DiveIndex = index,
            ModelVersion = "rules-1.0",
            ReasonCodes = nameof(ReasonCode.CalmLake),
        });
    }
}
