using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.Susv.Commands;
using Tauchplaetze.Core.Features.Susv.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Susv;

/// <remarks>
/// Ein Lauf des Imports mit den echten Antworten liefert die Einträge. «Arth, Tauchplatz Strick» (49178) ist eine Einschränkung,
/// «Arth, Tauchplatz Strick» hat keinen Defibrillator. Für ein Projekt dient der erste Defibrillator der Quelle.
/// </remarks>
public class SusvReviewTests(SqlServer sql) : SqlTest(sql)
{
    private const int Strick = 49178;

    [SqlTheory]
    [InlineData(null, null, null, false, SusvReviewError.KindRequired)]
    [InlineData(NoticeKind.Closure, "2026-10-15", null, true, SusvReviewError.YearlyNeedsDates)]
    [InlineData(NoticeKind.Closure, "2026-12-15", "2026-10-15", false, SusvReviewError.InvalidPeriod)]
    [InlineData(NoticeKind.Closure, "2026-10-15", "2026-12-15", true, null)]
    [InlineData(NoticeKind.Warning, "2026-11-01", "2026-03-31", true, null)]
    [InlineData(NoticeKind.Info, null, null, false, null)]
    public async Task Eine_Einschraenkung_braucht_eine_Art_und_jedes_Jahr_braucht_beide_Daten(NoticeKind? kind, string? from, string? until, bool yearly, SusvReviewError? expected)
    {
        // Arrange
        var (admin, site) = await Prepare();
        var entry = await EntryId(Strick, SusvTopic.Restriction);

        // Act
        var result = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(
            admin, entry, SusvReviewAction.Confirm, [site], kind, Date(from), Date(until), yearly));

        // Assert
        Assert.Equal(expected, result.Error);
    }

    [SqlFact]
    public async Task Ein_Projekt_hat_keine_Art_und_keinen_Zeitraum()
    {
        // Arrange
        var (admin, site) = await Prepare();
        var defibrillator = await AnyEntryId(SusvTopic.Defibrillator);

        // Act
        var withKind = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, defibrillator, SusvReviewAction.Confirm, [site], NoticeKind.Info));
        var plain = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, defibrillator, SusvReviewAction.Confirm, [site]));

        // Assert
        Assert.Equal((SusvReviewError.KindNotAllowed, (SusvReviewError?)null), (withKind.Error, plain.Error));
    }

    [SqlFact]
    public async Task Bestaetigen_braucht_einen_veroeffentlichten_Platz()
    {
        // Arrange
        var (admin, _) = await Prepare();
        var entry = await EntryId(Strick, SusvTopic.Restriction);

        // Act
        var none = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, entry, SusvReviewAction.Confirm, [], NoticeKind.Info));
        var unknown = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, entry, SusvReviewAction.Confirm, [Guid.NewGuid()], NoticeKind.Info));

        // Assert
        Assert.Equal((SusvReviewError.NoSites, SusvReviewError.UnknownSite), (none.Error!.Value, unknown.Error!.Value));
    }

    [SqlFact]
    public async Task Mitwirkende_duerfen_Eintraege_weder_sehen_noch_pruefen()
    {
        // Arrange
        var (_, site) = await Prepare();
        var contributor = await RegisterConfirmed("anna@example.org", "Anna");
        var entry = await EntryId(Strick, SusvTopic.Restriction);

        // Act
        var list = await Query<GetSusvEntriesForReviewQuery, SusvReviewList?>(new GetSusvEntriesForReviewQuery(contributor, "de"));
        var count = await Query<GetSusvReviewCountQuery, int>(new GetSusvReviewCountQuery(contributor));
        var result = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(contributor, entry, SusvReviewAction.Confirm, [site], NoticeKind.Info));

        // Assert
        Assert.Null(list);
        Assert.Equal(0, count);
        Assert.Equal(SusvReviewError.NotAllowed, result.Error);
    }

    [SqlFact]
    public async Task Vorgeschlagen_sind_die_veroeffentlichten_Plaetze_im_Umkreis_von_500_m_der_naechste_zuerst()
    {
        // Arrange
        var (admin, near) = await Prepare();
        var farther = await Site(admin, "Strick Süd", 47.0640, 8.5230);
        await Site(admin, "Arth Bahnhof", 47.0560, 8.5200);
        var entry = await EntryId(Strick, SusvTopic.Restriction);

        // Act
        var list = await Query<GetSusvEntriesForReviewQuery, SusvReviewList?>(new GetSusvEntriesForReviewQuery(admin, "fr"));

        // Assert
        var item = list!.Entries.Single(e => e.Id == entry);
        Assert.Equal([near, farther], item.Suggestions.Select(s => s.Id));
        Assert.True(item.Suggestions[0].DistanceMeters < 50);
        Assert.Equal("Arth, site de plongée Strick", item.Title);
    }

    [SqlFact]
    public async Task Ignorieren_nimmt_den_Eintrag_aus_der_Zahl_und_laesst_sich_zuruecknehmen()
    {
        // Arrange
        var (admin, _) = await Prepare();
        var entry = await EntryId(Strick, SusvTopic.Restriction);
        var before = await Query<GetSusvReviewCountQuery, int>(new GetSusvReviewCountQuery(admin));

        // Act
        await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, entry, SusvReviewAction.Ignore));
        var ignored = await Query<GetSusvReviewCountQuery, int>(new GetSusvReviewCountQuery(admin));
        await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, entry, SusvReviewAction.Reopen));
        var reopened = await Query<GetSusvReviewCountQuery, int>(new GetSusvReviewCountQuery(admin));

        // Assert
        Assert.Equal((before - 1, before), (ignored, reopened));
    }

    [SqlFact]
    public async Task Platz_anlegen_schlaegt_den_Namen_ohne_Vorspann_und_das_Gewaesser_vor_nur_der_Moderation()
    {
        // Arrange
        var (admin, _) = await Prepare();
        var contributor = await RegisterConfirmed("anna@example.org", "Anna");
        var entry = await EntryIdByTitle("SUSV Defibrillator – Boudry Plage, Neuenburgersee");

        // Act
        var draft = await Query<GetSusvSiteDraftQuery, SusvSiteDraft?>(new GetSusvSiteDraftQuery(admin, entry));
        var denied = await Query<GetSusvSiteDraftQuery, SusvSiteDraft?>(new GetSusvSiteDraftQuery(contributor, entry));

        // Assert
        var lakes = await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"));
        Assert.Equal("Boudry Plage, Neuenburgersee", draft!.Name);
        Assert.Equal("neuenburgersee", lakes.Single(w => w.Id == draft.WaterBodyId).Slug);
        Assert.Null(denied);
    }

    private async Task<Guid> EntryIdByTitle(string title)
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().SusvEntries
            .Where(e => e.Translations.Any(t => t.Culture == "de" && t.Title == title)).Select(e => e.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    [SqlFact]
    public async Task Waehrend_des_Abgleichs_wartet_die_Pruefung_statt_denselben_Eintrag_zu_ueberschreiben()
    {
        // Arrange
        var (admin, site) = await Prepare();
        var entry = await EntryId(Strick, SusvTopic.Restriction);
        await using var import = await Sql.RunLock.TryAcquire(ImportSusvEntriesHandler.LockName, TestContext.Current.CancellationToken);

        // Act
        var result = await Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(admin, entry, SusvReviewAction.Confirm, [site], NoticeKind.Info));

        // Assert
        Assert.Equal(SusvReviewError.Busy, result.Error);
    }

    private async Task<(string Admin, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var site = await Site(admin, "Strick", 47.0662, 8.5245);
        await Command<ImportSusvEntriesCommand, ImportSusvEntriesResult>(new ImportSusvEntriesCommand());
        return (admin, site);
    }

    private async Task<Guid> Site(string admin, string name, double latitude, double longitude)
    {
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zugersee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, name, lake, latitude, longitude, "Arth", "SZ", null));
        return result.DiveSiteId!.Value;
    }

    private async Task<Guid> EntryId(int sourceId, SusvTopic topic)
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().SusvEntries
            .Where(e => e.SourceId == sourceId && e.Topic == topic).Select(e => e.Id).SingleAsync(TestContext.Current.CancellationToken);
    }

    private async Task<Guid> AnyEntryId(SusvTopic topic)
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().SusvEntries
            .Where(e => e.Topic == topic).OrderBy(e => e.SourceId).Select(e => e.Id).FirstAsync(TestContext.Current.CancellationToken);
    }

    private static DateOnly? Date(string? value) => value is null ? null : DateOnly.Parse(value, System.Globalization.CultureInfo.InvariantCulture);
}
