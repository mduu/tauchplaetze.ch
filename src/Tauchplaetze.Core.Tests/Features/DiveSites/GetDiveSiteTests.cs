using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

/// <remarks>Die Ausgangslage entsteht direkt in der Datenbank. Für Entwurf, Archiv und alten Slug gibt es noch keinen Command.</remarks>
public class GetDiveSiteTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_veroeffentlichter_Platz_kommt_mit_dem_Gewaesser_in_der_Sprache_der_Seite()
    {
        // Arrange
        await Sql.Arrange(db => Add(db, "riff-williams-herrliberg", DiveSiteStatus.Published));

        // Act
        var result = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "fr"));

        // Assert
        var site = Assert.IsType<DiveSiteLookup.Found>(result).Site;
        Assert.Equal(("Riff Williams", "lac de Zurich", 47.2849, 8.6085), (site.Name, site.WaterBodyName, site.Latitude, site.Longitude));
    }

    [SqlFact]
    public async Task Einstiege_kommen_in_ihrer_Reihenfolge_mit_dem_Namen_der_Sprache_sonst_auf_Deutsch_sonst_ohne_Namen()
    {
        // Arrange
        await Sql.Arrange(async db =>
        {
            var site = await Add(db, "riff-williams-herrliberg", DiveSiteStatus.Published);
            site.EntryPoints.Add(EntryPoint(2, EntryPointKind.Exit, ("de", "Leiter")));
            site.EntryPoints.Add(EntryPoint(0, EntryPointKind.EntryAndExit, ("de", "Treppe"), ("fr", "Escalier")));
            site.EntryPoints.Add(EntryPoint(1, EntryPointKind.Entry, ("fr", "")));
        });

        // Act
        var result = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "fr"));

        // Assert
        var site = Assert.IsType<DiveSiteLookup.Found>(result).Site;
        Assert.Equal(
            [(EntryPointKind.EntryAndExit, "Escalier"), (EntryPointKind.Entry, null), (EntryPointKind.Exit, "Leiter")],
            site.EntryPoints.Select(e => (e.Kind, e.Name)));
    }

    [SqlFact]
    public async Task Ein_alter_Slug_fuehrt_auf_den_aktuellen()
    {
        // Arrange
        await Sql.Arrange(async db =>
        {
            var site = await Add(db, "riff-williams-herrliberg", DiveSiteStatus.Published);
            db.SlugRedirects.Add(new SlugRedirect { OldSlug = "riff-williams", DiveSiteId = site.Id });
        });

        // Act
        var result = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams", "de"));

        // Assert
        Assert.Equal(new DiveSiteLookup.Moved("riff-williams-herrliberg"), result);
    }

    [SqlTheory]
    [InlineData(DiveSiteStatus.Draft, typeof(DiveSiteLookup.NotFound))]
    [InlineData(DiveSiteStatus.Archived, typeof(DiveSiteLookup.Gone))]
    public async Task Entwuerfe_sind_nicht_oeffentlich_und_archivierte_Plaetze_sind_weg(DiveSiteStatus status, Type expected)
    {
        // Arrange
        await Sql.Arrange(db => Add(db, "platz", status));

        // Act
        var result = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("platz", "de"));

        // Assert
        Assert.IsType(expected, result);
    }

    [SqlFact]
    public async Task Ein_unbekannter_Slug_ergibt_nicht_gefunden()
    {
        // Act
        var result = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("gibt-es-nicht", "de"));

        // Assert
        Assert.IsType<DiveSiteLookup.NotFound>(result);
    }

    private static DiveSiteEntryPoint EntryPoint(int sortOrder, EntryPointKind kind, params (string Culture, string Name)[] names)
    {
        var id = Guid.CreateVersion7();
        return new DiveSiteEntryPoint
        {
            Id = id,
            Kind = kind,
            SortOrder = sortOrder,
            Location = GeoPoints.Create(47.285, 8.608),
            Translations = [.. names.Select(n => new DiveSiteEntryPointTranslation { EntryPointId = id, Culture = n.Culture, Name = n.Name })],
        };
    }

    private static Task<DiveSite> Add(TauchplaetzeDbContext db, string slug, DiveSiteStatus status)
    {
        var lake = new WaterBody
        {
            Slug = "zuerichsee",
            NameDe = "Zürichsee",
            NameFr = "lac de Zurich",
            WaterBodyType = WaterBodyType.Lake,
            Centroid = GeoPoints.Create(47.26, 8.68),
            IsActive = true,
        };
        var site = new DiveSite
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            Name = "Riff Williams",
            WaterBody = lake,
            Location = GeoPoints.Create(47.2849, 8.6085),
            Municipality = "Herrliberg",
            Canton = "ZH",
            Status = status,
        };
        db.DiveSites.Add(site);
        return Task.FromResult(site);
    }
}
