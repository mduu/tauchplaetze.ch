using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Editorial;

public class ImportDiveSitesTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_importierter_Platz_ist_veroeffentlicht_mit_freigegebener_Version_1_und_ein_zweiter_Import_aendert_nichts()
    {
        // Arrange
        var editorId = await PrepareWaterBodiesAndAdmin();

        // Act
        var first = await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(editorId));
        var second = await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(editorId));

        // Assert
        Assert.True(first.Added > 0);
        Assert.Equal(0, second.Added);

        await using var scope = Sql.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>();
        var site = await dbContext.DiveSites.SingleAsync(d => d.Slug == "riff-williams-herrliberg", TestContext.Current.CancellationToken);
        var revision = await dbContext.DiveSiteRevisions.SingleAsync(r => r.DiveSiteId == site.Id, TestContext.Current.CancellationToken);

        Assert.Equal(DiveSiteStatus.Published, site.Status);
        Assert.Equal((1, RevisionStatus.Approved, editorId, editorId), (revision.Number, revision.Status, revision.AuthorId, revision.ReviewerId));
        Assert.Equal(revision.SnapshotHash, DiveSiteSnapshot.HashOf(await Sql.Revisions.Load(revision.SnapshotPath, TestContext.Current.CancellationToken)));
    }

    [SqlFact]
    public async Task Der_Import_uebernimmt_Tiefe_Schwierigkeit_Steilwand_und_Zugang_als_Startwerte()
    {
        // Arrange
        var editorId = await PrepareWaterBodiesAndAdmin();

        // Act
        await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(editorId));

        // Assert
        await using var scope = Sql.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>();
        var site = await dbContext.DiveSites.SingleAsync(d => d.Slug == "chappeli-oberaegeri", TestContext.Current.CancellationToken);
        Assert.Equal((30, (byte?)2, (byte?)4, DiveSiteAccess.Shore), (site.MaxDepth, site.DifficultyRating, site.WallRating, site.Access));
    }

    [SqlFact]
    public async Task Ein_umbenannter_Platz_kommt_beim_naechsten_Import_nicht_wieder()
    {
        // Arrange
        var editorId = await PrepareWaterBodiesAndAdmin();
        await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(editorId));
        await Sql.Arrange(async db =>
        {
            var site = await db.DiveSites.SingleAsync(d => d.Slug == "riff-williams-herrliberg");
            site.Slug = "riff-williams-am-zuerichsee";
            db.SlugRedirects.Add(new SlugRedirect { OldSlug = "riff-williams-herrliberg", DiveSiteId = site.Id });
        });

        // Act
        var result = await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(editorId));

        // Assert
        Assert.Equal(0, result.Added);
    }

    private async Task<string> PrepareWaterBodiesAndAdmin()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admins = await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]));
        return admins.UserIds.Single();
    }
}
