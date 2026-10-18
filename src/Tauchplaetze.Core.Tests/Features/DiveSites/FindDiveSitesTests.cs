using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

/// <remarks>Die Uhr der Tests steht auf dem 17.10.2026.</remarks>
public class FindDiveSitesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlTheory]
    [InlineData("zurich")]
    [InlineData("ZUERICH")]
    [InlineData("Zürichsee")]
    [InlineData("lac de zur")]
    public async Task Die_Suche_findet_das_Gewaesser_in_jeder_Sprache_ohne_Gross_und_Kleinschreibung_Umlaute_und_Akzente(string text)
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var found = await Find(new FindDiveSitesQuery("de", Text: text));

        // Assert
        Assert.Equal(["Riff Williams", "Ermitage"], found.Sites.Select(s => s.Name));
    }

    [SqlFact]
    public async Task Die_Suche_findet_Name_und_Gemeinde()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var byName = await Find(new FindDiveSitesQuery("de", Text: "riff"));
        var byMunicipality = await Find(new FindDiveSitesQuery("de", Text: "kusnacht"));

        // Assert
        Assert.Equal(["Riff Williams"], byName.Sites.Select(s => s.Name));
        Assert.Equal(["Ermitage"], byMunicipality.Sites.Select(s => s.Name));
    }

    [SqlTheory]
    [InlineData(new[] { SiteTrait.Wall }, new[] { "Riff Williams" })]
    [InlineData(new[] { SiteTrait.Beginner }, new[] { "Ermitage" })]
    [InlineData(new[] { SiteTrait.River }, new[] { "Rheinplatz" })]
    [InlineData(new[] { SiteTrait.Wall, SiteTrait.Beginner }, new string[0])]
    public async Task Eigenschaften_filtern_und_mehrere_gelten_zusammen(SiteTrait[] traits, string[] expected)
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var found = await Find(new FindDiveSitesQuery("de", Traits: traits));

        // Assert
        Assert.Equal(expected, found.Sites.Select(s => s.Name));
    }

    [SqlFact]
    public async Task Ohne_Angabe_kommt_der_hoechste_Index_zuerst_und_Plaetze_ohne_Index_am_Ende()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var byIndex = await Find(new FindDiveSitesQuery("de"));
        var byName = await Find(new FindDiveSitesQuery("de", Sort: SiteSort.Name));

        // Assert
        Assert.Equal(SiteSort.Index, byIndex.Sort);
        Assert.Equal(["Riff Williams", "Ermitage", "Rheinplatz"], byIndex.Sites.Select(s => s.Name));
        Assert.Equal(["Ermitage", "Rheinplatz", "Riff Williams"], byName.Sites.Select(s => s.Name));
    }

    [SqlFact]
    public async Task Mit_Standort_sortiert_die_Suche_nach_Distanz_und_der_Umkreis_begrenzt()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var near = await Find(new FindDiveSitesQuery("de", Latitude: 47.55, Longitude: 7.79));
        var within = await Find(new FindDiveSitesQuery("de", Latitude: 47.55, Longitude: 7.79, RadiusKm: 10));

        // Assert
        Assert.Equal((SiteSort.Distance, "Rheinplatz"), (near.Sort, near.Sites[0].Name));
        Assert.InRange(near.Sites[0].DistanceKm!.Value, 0, 2);
        Assert.Equal(["Rheinplatz"], within.Sites.Select(s => s.Name));
    }

    [SqlFact]
    public async Task Die_Treffer_kommen_seitenweise_die_Karte_bekommt_alle_und_die_Zahlen_zaehlen_alle_oeffentlichen_Plaetze()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var second = await Find(new FindDiveSitesQuery("de", Page: 2, PageSize: 2));
        var beyond = await Find(new FindDiveSitesQuery("de", Page: 99, PageSize: 2));
        var filtered = await Find(new FindDiveSitesQuery("de", Text: "riff"));

        // Assert
        Assert.Equal((3, 2, 2, 1, 3), (second.Total, second.Page, second.PageCount, second.Sites.Count, second.MapSites.Count));
        Assert.Equal(2, beyond.Page);
        Assert.Equal((1, 3, 2), (filtered.Total, filtered.SiteCount, filtered.WaterBodyCount));
    }

    [SqlFact]
    public async Task Mit_Gewaesser_zeigt_die_Suche_nur_dessen_Plaetze_und_nennt_es()
    {
        // Arrange
        await Sql.Arrange(Seed);

        // Act
        var found = await Find(new FindDiveSitesQuery("fr", WaterBodySlug: "zuerichsee"));

        // Assert
        Assert.Equal(("lac de Zurich", 2), (found.WaterBodyName, found.Total));
    }

    private Task<FoundDiveSites> Find(FindDiveSitesQuery query) => Query<FindDiveSitesQuery, FoundDiveSites>(query);

    private static Task Seed(TauchplaetzeDbContext db)
    {
        var lake = new WaterBody { Slug = "zuerichsee", NameDe = "Zürichsee", NameFr = "lac de Zurich", WaterBodyType = WaterBodyType.Lake, Centroid = GeoPoints.Create(47.26, 8.68), IsActive = true };
        var river = new WaterBody { Slug = "rhein", NameDe = "Rhein", WaterBodyType = WaterBodyType.River, Centroid = GeoPoints.Create(47.56, 7.8), IsActive = true };
        var riff = Site(lake, "Riff Williams", "Herrliberg", 47.2849, 8.6085, DiveSiteStatus.Published);
        riff.WallRating = 4;
        var ermitage = Site(lake, "Ermitage", "Küsnacht", 47.31, 8.58, DiveSiteStatus.Published);
        ermitage.DifficultyRating = 2;
        db.DiveSites.AddRange(riff, ermitage, Site(river, "Rheinplatz", "Rheinfelden", 47.554, 7.795, DiveSiteStatus.Published), Site(lake, "Entwurf", null, 47.3, 8.6, DiveSiteStatus.Draft));
        db.DailyForecasts.Add(new DailyForecast { DiveSiteId = riff.Id, Date = new DateOnly(2026, 10, 17), DataLevel = 3, DiveIndex = 7.2m, ModelVersion = "rules-1.0" });
        db.DailyForecasts.Add(new DailyForecast { DiveSiteId = ermitage.Id, Date = new DateOnly(2026, 10, 17), DataLevel = 3, DiveIndex = 5.0m, ModelVersion = "rules-1.0" });
        return Task.CompletedTask;
    }

    private static DiveSite Site(WaterBody waterBody, string name, string? municipality, double latitude, double longitude, DiveSiteStatus status) => new()
    {
        Id = Guid.CreateVersion7(),
        Slug = name.ToLowerInvariant().Replace(' ', '-'),
        Name = name,
        Municipality = municipality,
        WaterBody = waterBody,
        Location = GeoPoints.Create(latitude, longitude),
        Status = status,
    };
}
