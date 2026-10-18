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

    private static DiveSite Site(WaterBody waterBody, string slug, DiveSiteStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = slug,
        Name = slug,
        WaterBody = waterBody,
        Location = new Point(8.6, 47.3) { SRID = 4326 },
        Status = status,
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
