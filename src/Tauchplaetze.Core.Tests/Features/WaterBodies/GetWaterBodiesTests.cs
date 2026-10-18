using NetTopologySuite.Geometries;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.WaterBodies;

public class GetWaterBodiesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Inaktive_Gewaesser_erscheinen_nicht()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            db.WaterBodies.AddRange(Lake("zuerichsee", "Zürichsee"), Lake("inaktiv", "Inaktiver See", isActive: false));
            return Task.CompletedTask;
        });

        // Act
        var result = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));

        // Assert
        Assert.Equal(["zuerichsee"], result.Select(w => w.Slug));
    }

    [SqlTheory]
    [InlineData("de", new[] { "Greifensee", "Rhein", "Zugersee", "Zürichsee" })]
    [InlineData("en", new[] { "Greifensee", "Lake Zug", "Lake Zurich", "Rhine" })]
    [InlineData("fr", new[] { "Greifensee", "lac de Zoug", "lac de Zurich", "Rhin" })]
    [InlineData("it", new[] { "Greifensee", "lago di Zugo", "lago di Zurigo", "Reno" })]
    public async Task Namen_kommen_in_der_Sprache_sortiert_und_fehlende_Uebersetzungen_auf_Deutsch(string culture, string[] expected)
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            db.WaterBodies.AddRange(
                Lake("zuerichsee", "Zürichsee", "Lake Zurich", "lac de Zurich", "lago di Zurigo"),
                Lake("zugersee", "Zugersee", "Lake Zug", "lac de Zoug", "lago di Zugo"),
                Lake("rhein", "Rhein", "Rhine", "Rhin", "Reno", WaterBodyType.River),
                Lake("greifensee", "Greifensee"));
            return Task.CompletedTask;
        });

        // Act
        var result = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery(culture));

        // Assert
        Assert.Equal(expected, result.Select(w => w.Name));
    }

    [SqlFact]
    public async Task Ein_Gewaesser_zaehlt_nur_veroeffentlichte_Plaetze()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var lake = Lake("zuerichsee", "Zürichsee");
            db.DiveSites.AddRange(Site(lake, "riff", DiveSiteStatus.Published), Site(lake, "entwurf", DiveSiteStatus.Draft), Site(lake, "alt", DiveSiteStatus.Archived));
            db.WaterBodies.Add(Lake("rhein", "Rhein", type: WaterBodyType.River));
            return Task.CompletedTask;
        });

        // Act
        var result = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));

        // Assert
        Assert.Equal([("rhein", 0), ("zuerichsee", 1)], result.Select(w => (w.Slug, w.SiteCount)));
    }

    [SqlFact]
    public async Task Die_Uebersicht_zeigt_zuerst_grosse_Seen_dann_Seen_nach_Region_der_meisten_Plaetze_dann_Fluesse_dann_Seen_ohne_Kanton()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var zurich = Lake("zuerichsee", "Zürichsee");
            var thun = Lake("thunersee", "Thunersee");
            var greifen = Lake("greifensee", "Greifensee");
            var hallwil = Lake("hallwilersee", "Hallwilersee");
            var walen = Lake("walensee", "Walensee");
            var lauerz = Lake("lauerzersee", "Lauerzersee");
            var empty = Lake("leer", "Leersee");
            var rhine = Lake("rhein", "Rhein", type: WaterBodyType.River);
            db.WaterBodies.Add(empty);
            db.DiveSites.AddRange(Sites(zurich, 9, "ZH"));
            db.DiveSites.AddRange(Sites(thun, 8, "BE"));
            db.DiveSites.AddRange(Sites(greifen, 2, "ZH"));
            db.DiveSites.AddRange([.. Sites(hallwil, 1, "AG"), .. Sites(hallwil, 2, "LU")]);
            db.DiveSites.AddRange([.. Sites(walen, 2, "GL"), .. Sites(walen, 2, "SG")]);
            db.DiveSites.AddRange(Sites(lauerz, 1, null));
            db.DiveSites.AddRange(Sites(rhine, 12, "BS"));
            return Task.CompletedTask;
        });

        // Act
        var groups = await Query<GetWaterBodyGroupsQuery, IReadOnlyList<WaterBodyGroup>>(new GetWaterBodyGroupsQuery("de"));

        // Assert
        Assert.Equal(
            [
                (WaterBodyGroupKind.LargeLakes, (SwissRegion?)null, "zuerichsee thunersee"),
                (WaterBodyGroupKind.Region, SwissRegion.Zurich, "greifensee"),
                (WaterBodyGroupKind.Region, SwissRegion.East, "walensee"),
                (WaterBodyGroupKind.Region, SwissRegion.Central, "hallwilersee"),
                (WaterBodyGroupKind.Rivers, null, "rhein"),
                (WaterBodyGroupKind.Other, null, "lauerzersee leer"),
            ],
            groups.Select(g => (g.Kind, g.Region, string.Join(" ", g.Waters.Select(w => w.Slug)))));
    }

    private static IEnumerable<DiveSite> Sites(WaterBody waterBody, int count, string? canton) =>
        Enumerable.Range(1, count).Select(i => Site(waterBody, $"{waterBody.Slug}-{canton}-{i}", DiveSiteStatus.Published, canton));

    private static DiveSite Site(WaterBody waterBody, string slug, DiveSiteStatus status, string? canton = null) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = slug.ToLowerInvariant(),
        Name = slug,
        WaterBody = waterBody,
        Location = new Point(8.6, 47.3) { SRID = 4326 },
        Status = status,
        Canton = canton,
    };

    private static WaterBody Lake(
        string slug,
        string nameDe,
        string? nameEn = null,
        string? nameFr = null,
        string? nameIt = null,
        WaterBodyType type = WaterBodyType.Lake,
        bool isActive = true) =>
        new()
        {
            Slug = slug,
            NameDe = nameDe,
            NameEn = nameEn,
            NameFr = nameFr,
            NameIt = nameIt,
            WaterBodyType = type,
            Centroid = new Point(8.6, 47.3) { SRID = 4326 },
            IsActive = isActive,
        };
}
