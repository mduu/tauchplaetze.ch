using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.DiveSites;

public class GetSitemapEntriesTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateTimeOffset Older = new(2026, 10, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newer = new(2026, 10, 18, 8, 0, 0, TimeSpan.Zero);

    [SqlFact]
    public async Task Die_Sitemap_nennt_nur_veroeffentlichte_Plaetze_und_Gewaesser_mit_veroeffentlichten_Plaetzen()
    {
        // Arrange
        await Sql.Arrange(db =>
        {
            var zurich = Lake("zuerichsee");
            var zug = Lake("zugersee");
            Site(db, "riff-williams-herrliberg", zurich, DiveSiteStatus.Published, Older);
            Site(db, "wassersportzentrum-zuerich", zurich, DiveSiteStatus.Published, Newer);
            Site(db, "alter-steg", zurich, DiveSiteStatus.Archived, Newer.AddDays(1));
            Site(db, "neuer-vorschlag", zug, DiveSiteStatus.Draft, Newer);
            return Task.CompletedTask;
        });

        // Act
        var entries = await Query<GetSitemapEntriesQuery, SitemapEntries>(new GetSitemapEntriesQuery());

        // Assert
        Assert.Equal(["riff-williams-herrliberg", "wassersportzentrum-zuerich"], entries.DiveSites.Select(s => s.Slug));
        Assert.Equal([new SitemapEntry("zuerichsee", Newer)], entries.WaterBodies);
    }

    private static WaterBody Lake(string slug) => new()
    {
        Slug = slug,
        NameDe = slug,
        WaterBodyType = WaterBodyType.Lake,
        Centroid = GeoPoints.Create(47.26, 8.68),
        IsActive = true,
    };

    private static void Site(TauchplaetzeDbContext db, string slug, WaterBody lake, DiveSiteStatus status, DateTimeOffset updatedAt) =>
        db.DiveSites.Add(new DiveSite
        {
            Id = Guid.CreateVersion7(),
            Slug = slug,
            Name = slug,
            WaterBody = lake,
            Location = GeoPoints.Create(47.2849, 8.6085),
            Status = status,
            UpdatedAt = updatedAt,
        });
}
