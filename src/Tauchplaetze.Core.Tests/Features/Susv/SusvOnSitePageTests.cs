using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.Susv.Commands;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Susv;

/// <remarks>
/// Story 003. Der Import liest die echten Antworten. «Arth, Tauchplatz Strick» (49178) ist eine Einschränkung.
/// Heute ist der 17.10.2026, die Sperrung gilt jedes Jahr vom 15.10. bis 15.12.
/// </remarks>
public class SusvOnSitePageTests(SqlServer sql) : SqlTest(sql)
{
    private const int Strick = 49178;

    [SqlFact]
    public async Task Eine_bestaetigte_Sperrung_steht_im_Banner_mit_Titel_Link_und_Ende_und_sperrt_die_Tage_der_Prognose()
    {
        // Arrange
        var (admin, site, slug) = await Prepare();

        // Act
        await Confirm(admin, site, NoticeKind.Closure);
        var details = await Site(slug, "fr");

        // Assert
        var notice = Assert.Single(details.Notices);
        Assert.Equal((NoticeKind.Closure, NoticeTiming.Today, new DateOnly(2026, 12, 15), true), (notice.Kind, notice.Timing, notice.ValidUntil, notice.RecursYearly));
        Assert.Equal("Arth, site de plongée Strick", notice.Susv!.Title);
        Assert.StartsWith("https://susv.ch/fr/", notice.Susv.DetailUrl);
        Assert.InRange(notice.Text.Length, 1, SusvRules.BannerTextLength);
        Assert.Equal(5, details.ClosedDayList.Count);
    }

    [SqlFact]
    public async Task Auf_Englisch_steht_die_automatische_Uebersetzung_aus_dem_Deutschen()
    {
        // Arrange
        var (admin, site, slug) = await Prepare();

        // Act
        await Confirm(admin, site, NoticeKind.Closure);
        var english = Assert.Single((await Site(slug, "en")).Notices);

        // Assert
        Assert.Equal(("Arth, Tauchplatz Strick (en)", false, true), (english.Susv!.Title, english.GermanOnly, english.MachineTranslated));
        Assert.StartsWith("https://susv.ch/de/", english.Susv.DetailUrl);
    }

    [SqlFact]
    public async Task Scheitert_die_Uebersetzung_steht_auf_Englisch_der_deutsche_Text()
    {
        // Arrange
        var (admin, site, slug) = await Prepare();
        Sql.Translator.Translate = (_, _) => throw new HttpRequestException("Claude API nicht erreichbar");

        // Act
        await Confirm(admin, site, NoticeKind.Closure);
        var english = Assert.Single((await Site(slug, "en")).Notices);

        // Assert
        Assert.Equal(("Arth, Tauchplatz Strick", true, false), (english.Susv!.Title, english.GermanOnly, english.MachineTranslated));
    }

    [SqlFact]
    public async Task Ein_ignorierter_Eintrag_erscheint_nicht()
    {
        // Arrange
        var (admin, site, slug) = await Prepare();
        await Confirm(admin, site, NoticeKind.Warning);

        // Act
        await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, await EntryId(), SusvReviewAction.Ignore));
        var details = await Site(slug, "de");

        // Assert
        Assert.Empty(details.Notices);
    }

    private async Task Confirm(string admin, Guid site, NoticeKind kind)
    {
        var result = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(
            admin, await EntryId(), SusvReviewAction.Confirm, [site], kind, new DateOnly(2026, 10, 15), new DateOnly(2026, 12, 15), RecursYearly: true));
        Assert.Null(result.Error);
    }

    private async Task<DiveSiteDetails> Site(string slug, string culture) =>
        ((DiveSiteLookup.Found)await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery(slug, culture))).Site;

    private async Task<(string Admin, Guid Site, string Slug)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zugersee").Id;
        var site = (await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, "Strick", lake, 47.0662, 8.5245, "Arth", "SZ", null))).DiveSiteId!.Value;
        await Command<ImportSusvEntriesCommand, ImportSusvEntriesResult>(new ImportSusvEntriesCommand());

        await using var scope = Sql.Services.CreateAsyncScope();
        var slug = await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().DiveSites
            .Where(d => d.Id == site).Select(d => d.Slug).SingleAsync(TestContext.Current.CancellationToken);
        return (admin, site, slug);
    }

    private async Task<Guid> EntryId()
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().SusvEntries
            .Where(e => e.SourceId == Strick && e.Topic == SusvTopic.Restriction).Select(e => e.Id).SingleAsync(TestContext.Current.CancellationToken);
    }
}
