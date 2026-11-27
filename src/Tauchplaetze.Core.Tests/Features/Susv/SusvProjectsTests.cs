using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Susv;

/// <remarks>Story 004. Die Einträge entstehen direkt in der Datenbank, mit Link pro Sprache.</remarks>
public class SusvProjectsTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Die_Platzseite_zeigt_nur_bestaetigte_nicht_entfernte_Projekte_mit_Link_in_der_Sprache_ohne_die_Ausbaustufe_zu_aendern()
    {
        // Arrange
        var site = await Prepare();
        await Sql.Arrange(db =>
        {
            Add(db, site, 1, SusvTopic.Defibrillator, SusvReviewStatus.Confirmed);
            Add(db, site, 2, SusvTopic.MobileToilet, SusvReviewStatus.Pending);
            Add(db, site, 3, SusvTopic.InclusiveSite, SusvReviewStatus.Confirmed);
            Add(db, site, 4, SusvTopic.SupportProject, SusvReviewStatus.Confirmed, removed: true);
            return Task.CompletedTask;
        });

        // Act
        var details = ((DiveSiteLookup.Found)await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "fr"))).Site;

        // Assert
        Assert.Equal([SusvTopic.Defibrillator, SusvTopic.InclusiveSite], details.SusvProjectList.Select(p => p.Topic));
        Assert.Equal("https://susv.ch/fr/1/", details.SusvProject(SusvTopic.Defibrillator)!.DetailUrl);
        Assert.Equal((0, true), (details.ContentLevel, details.IsDirectoryOnly));
    }

    [SqlFact]
    public async Task Der_Filter_Inklusion_findet_nur_Plaetze_mit_bestaetigtem_Inklusions_Tauchplatz()
    {
        // Arrange
        var site = await Prepare();
        await Sql.Arrange(db =>
        {
            Add(db, site, 3, SusvTopic.InclusiveSite, SusvReviewStatus.Pending);
            return Task.CompletedTask;
        });
        var pending = await Query<FindDiveSitesQuery, FoundDiveSites>(new FindDiveSitesQuery("de", Traits: [SiteTrait.Inclusive]));
        await Sql.Arrange(db => db.SusvEntries.Where(e => e.SourceId == 3).ExecuteUpdateAsync(e => e.SetProperty(x => x.ReviewStatus, SusvReviewStatus.Confirmed)));

        // Act
        var confirmed = await Query<FindDiveSitesQuery, FoundDiveSites>(new FindDiveSitesQuery("de", Traits: [SiteTrait.Inclusive]));

        // Assert
        Assert.Empty(pending.Sites);
        var found = Assert.Single(confirmed.Sites);
        Assert.Contains(SiteTrait.Inclusive, found.Traits);
    }

    private async Task<Guid> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        return await AddRiffWilliams(admin);
    }

    private static void Add(TauchplaetzeDbContext db, Guid site, int sourceId, SusvTopic topic, SusvReviewStatus status, bool removed = false)
    {
        var id = Guid.CreateVersion7();
        db.SusvEntries.Add(new SusvEntry
        {
            Id = id,
            SourceId = sourceId,
            Topic = topic,
            Location = GeoPoints.Create(47.2849, 8.6085),
            ContentHash = "x",
            ReviewStatus = status,
            RemovedAt = removed ? DateTimeOffset.UtcNow : null,
            Translations =
            [
                new() { Culture = "de", Title = "Eintrag", DetailUrl = $"https://susv.ch/de/{sourceId}/" },
                new() { Culture = "fr", Title = "Entrée", DetailUrl = $"https://susv.ch/fr/{sourceId}/" },
            ],
            Sites = [new() { EntryId = id, DiveSiteId = site }],
        });
    }
}
