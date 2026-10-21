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

    [SqlFact]
    public async Task Ein_gesperrter_Platz_fehlt_in_der_Rangliste_nur_an_den_gesperrten_Tagen()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
            var riff = Add(db, lake, "riff-williams", 47.2849, 8.6085, 8.4m);
            Add(db, lake, "genf-plage", 46.21, 6.15, 6.1m);
            AddForecast(db, riff, Today.AddDays(1), 8.0m);
            riff.Notices.Add(new DiveSiteNotice { Id = Guid.CreateVersion7(), Kind = NoticeKind.Closure, ValidUntil = Today, Translations = [new() { Culture = "de", Text = "Gesperrt" }] });
            riff.Notices.Add(new DiveSiteNotice { Id = Guid.CreateVersion7(), Kind = NoticeKind.Warning, Translations = [new() { Culture = "de", Text = "Parkplatz zu" }] });
            return Task.CompletedTask;
        });

        // Act
        var today = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));
        var tomorrow = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de", Today.AddDays(1)));

        // Assert
        Assert.Equal(["genf-plage"], today.Ranking.Select(r => r.Slug));
        Assert.Equal(["riff-williams"], tomorrow.Ranking.Select(r => r.Slug));
    }

    [SqlFact]
    public async Task Solange_wenige_Plaetze_einen_Index_haben_nennt_die_Rangliste_die_Gewaesser_mit_Prognose()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        var coverage = Assert.IsType<ForecastCoverage>(best.Coverage);
        Assert.Equal(["See"], coverage.WaterBodyNames);
        Assert.Equal((2, 3), (coverage.SitesWithIndex, coverage.Sites));
    }

    [SqlFact]
    public async Task Ab_80_Prozent_der_Plaetze_mit_Index_entfaellt_der_Hinweis_auf_die_Gewaesser()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
            for (var i = 0; i < 4; i++)
            {
                Add(db, lake, $"mit-index-{i}", 47.2 + i * 0.01, 8.6, 7m);
            }

            Add(db, lake, "ohne-index", 47.3, 8.6, null);
            return Task.CompletedTask;
        });

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        Assert.Null(best.Coverage);
    }

    [SqlFact]
    public async Task Plaetze_ohne_Index_im_Umkreis_stehen_ohne_Rang_neben_der_Rangliste_naechster_zuerst()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
            Add(db, lake, "riff-williams", 47.2849, 8.6085, 8.4m);
            Add(db, lake, "weit-weg", 47.33, 8.75, null);
            Add(db, lake, "nah", 47.36, 8.55, null);
            Add(db, lake, "ausserhalb", 46.21, 6.15, null);
            return Task.CompletedTask;
        });
        var zurich = new Area(47.37, 8.54, 25);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de", Area: zurich));

        // Assert
        Assert.Equal(["riff-williams"], best.Ranking.Select(r => r.Slug));
        Assert.Equal(["nah", "weit-weg"], best.WithoutForecast!.Select(s => s.Slug));
        Assert.Equal(2, best.WithoutForecastCount);
    }

    [SqlFact]
    public async Task Die_Rangliste_zeigt_hoechstens_zwei_Plaetze_pro_Gewaesser_und_die_weiteren_beim_zweiten()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var genfersee = Lake("genfersee");
            var zuerichsee = Lake("zuerichsee");
            Add(db, genfersee, "genf-1", 46.21, 6.15, 9.0m);
            Add(db, genfersee, "genf-2", 46.22, 6.16, 8.8m);
            Add(db, genfersee, "genf-3", 46.23, 6.17, 8.6m);
            Add(db, genfersee, "genf-4", 46.24, 6.18, 8.4m);
            Add(db, zuerichsee, "zuerich-1", 47.28, 8.61, 8.0m);
            return Task.CompletedTask;
        });

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        Assert.Equal(["genf-1", "genf-2", "zuerich-1"], best.Ranking.Select(r => r.Slug));
        Assert.Equal(["genf-3", "genf-4"], best.Ranking[1].MoreList.Select(r => r.Slug));
        Assert.Empty(best.Ranking[0].MoreList);
        Assert.Empty(best.Ranking[2].MoreList);
    }

    [SqlFact]
    public async Task Mit_Ort_zieht_die_Distanz_von_der_Reihung_ab_der_angezeigte_Index_bleibt()
    {
        // Arrange
        await Sql.Arrange(DistanceSeed);
        var zurich = new Area(47.37, 8.54, 40);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de", Area: zurich));

        // Assert
        Assert.Equal(["nah", "weit"], best.Ranking.Select(r => r.Slug));
        Assert.Equal(8.0m, best.Ranking[1].DiveIndex);
    }

    [SqlFact]
    public async Task Ohne_Ort_zaehlt_nur_der_Index()
    {
        // Arrange
        await Sql.Arrange(DistanceSeed);

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        Assert.Equal(["weit", "nah"], best.Ranking.Select(r => r.Slug));
    }

    [SqlTheory]
    [InlineData(1796, 1796)]
    [InlineData(700, 700)]
    [InlineData(699, null)]
    [InlineData(null, null)]
    public async Task Ein_See_ab_700_m_ueber_Meer_gilt_als_Bergsee(int? elevation, int? expected)
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var lake = Lake("silsersee");
            lake.Elevation = elevation;
            Add(db, lake, "sils", 46.42, 9.74, 8.9m);
            return Task.CompletedTask;
        });

        // Act
        var best = await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"));

        // Assert
        Assert.Equal(expected, Assert.Single(best.Ranking).MountainLakeElevation);
    }

    [SqlTheory]
    [InlineData(0.1, 0.3, false)]
    [InlineData(0.0, 0.0, false)]
    [InlineData(14.0, 17.0, true)]
    public async Task Eine_gespeicherte_Sprungschicht_an_der_Oberflaeche_erscheint_beim_Lesen_nicht(double top, double bottom, bool shown)
    {
        // Arrange
        Guid siteId = default;
        await Sql.Arrange(db =>
        {
            var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
            var site = Add(db, lake, "sisikon", 46.95, 8.62, null);
            siteId = site.Id;
            db.DailyForecasts.Local.Single().DiveIndex = 9.1m;
            db.DailyForecasts.Local.Single().ThermoclineTop = (decimal)top;
            db.DailyForecasts.Local.Single().ThermoclineBottom = (decimal)bottom;
            db.DailyForecasts.Local.Single().VisibilityGoodBelow = (decimal)bottom;
            db.DailyForecasts.Local.Single().ReasonCodes = $"{nameof(ReasonCode.ClearBelowThermocline)},{nameof(ReasonCode.CalmLake)}";
            return Task.CompletedTask;
        });

        // Act
        var day = (await Query<GetSiteForecastQuery, SiteForecast>(new GetSiteForecastQuery(siteId))).Days[0];
        var ranked = Assert.Single((await Query<GetBestSitesQuery, BestSites>(new GetBestSitesQuery("de"))).Ranking);

        // Assert
        Assert.Equal(shown, day.ThermoclineTop is not null);
        Assert.Equal(shown, day.VisibilityGoodBelow is not null);
        Assert.Equal(shown, day.Reasons.Contains(ReasonCode.ClearBelowThermocline));
        Assert.Equal(shown, ranked.ThermoclineBottom is not null);
        Assert.Equal(shown, ranked.Reasons.Contains(ReasonCode.ClearBelowThermocline));
        Assert.Contains(ReasonCode.CalmLake, day.Reasons);
    }

    private static Task DistanceSeed(TauchplaetzeDbContext db)
    {
        Add(db, Lake("zuerichsee"), "nah", 47.355, 8.555, 7.8m);
        Add(db, Lake("zugersee"), "weit", 47.10, 8.48, 8.0m);
        return Task.CompletedTask;
    }

    private static WaterBody Lake(string slug) =>
        new() { Slug = slug, NameDe = slug, WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };

    private static Task Seed(TauchplaetzeDbContext db)
    {
        var lake = new WaterBody { Slug = "see", NameDe = "See", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47, 8), IsActive = true };
        Add(db, lake, "riff-williams", 47.2849, 8.6085, 8.4m);
        Add(db, lake, "genf-plage", 46.21, 6.15, 6.1m);
        Add(db, lake, "nur-wetter", 47.30, 8.60, null);
        return Task.CompletedTask;
    }

    private static DiveSite Add(TauchplaetzeDbContext db, WaterBody lake, string slug, double latitude, double longitude, decimal? index)
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
        AddForecast(db, site, Today, index);
        return site;
    }

    private static void AddForecast(TauchplaetzeDbContext db, DiveSite site, DateOnly date, decimal? index) =>
        db.DailyForecasts.Add(new DailyForecast
        {
            DiveSiteId = site.Id,
            Date = date,
            DataLevel = index is null ? (byte)1 : (byte)3,
            DiveIndex = index,
            ModelVersion = "rules-1.0",
            ReasonCodes = nameof(ReasonCode.CalmLake),
        });
}
