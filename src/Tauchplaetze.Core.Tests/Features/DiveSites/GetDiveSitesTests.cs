using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

public class GetDiveSitesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Das_Verzeichnis_zeigt_nur_oeffentliche_Plaetze_mit_dem_Index_von_heute()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var sites = await Query<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>(new GetDiveSitesQuery("de"));

        // Assert
        Assert.Equal(["Rheinplatz", "Riff Williams"], sites.Select(s => s.Name));
        Assert.Equal(7.2m, sites.Single(s => s.Name == "Riff Williams").DiveIndexToday);
        Assert.Null(sites.Single(s => s.Name == "Rheinplatz").DiveIndexToday);
    }

    [SqlFact]
    public async Task Mit_Gewaesser_zeigt_das_Verzeichnis_nur_dessen_Plaetze()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var sites = await Query<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>(new GetDiveSitesQuery("de", "rhein"));

        // Assert
        Assert.Equal(["Rheinplatz"], sites.Select(s => s.Name));
    }

    private static Task Seed(TauchplaetzeDbContext db)
    {
        var lake = new WaterBody { Slug = "zuerichsee", NameDe = "Zürichsee", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47.26, 8.68), IsActive = true };
        var river = new WaterBody { Slug = "rhein", NameDe = "Rhein", WaterBodyType = WaterBodyType.River, Centroid = GeoPoints.Create(47.56, 7.8), IsActive = true };
        var riff = Site(lake, "Riff Williams", DiveSiteStatus.Published);
        db.DiveSites.AddRange(riff, Site(river, "Rheinplatz", DiveSiteStatus.Published), Site(lake, "Entwurf", DiveSiteStatus.Draft));
        db.DailyForecasts.Add(new DailyForecast { DiveSiteId = riff.Id, Date = new DateOnly(2026, 10, 17), DataLevel = 3, DiveIndex = 7.2m, ModelVersion = "rules-1.0" });
        return Task.CompletedTask;
    }

    private static DiveSite Site(WaterBody waterBody, string name, DiveSiteStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Name = name,
        WaterBody = waterBody,
        Location = GeoPoints.Create(47.3, 8.6),
        Status = status,
    };
}
