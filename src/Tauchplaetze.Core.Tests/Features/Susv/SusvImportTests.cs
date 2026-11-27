using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Echolot;
using Tauchplaetze.Core.Features.Echolot.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Susv;
using Tauchplaetze.Core.Features.Susv.Commands;
using Tauchplaetze.Core.Features.Susv.Queries;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Features.Forecasts;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Susv;

/// <remarks>
/// Die Antworten der Karten und die Detailseite von «Arth, Tauchplatz Strick» (49178) sind echt (26.11.2026).
/// 60 Einträge in fünf Themen. Detailseiten holt der Import nur für die 23 Einschränkungen. Die übrigen antworten mit 404, dann gilt der Kurztext.
/// </remarks>
public class SusvImportTests(SqlServer sql) : SqlTest(sql)
{
    private const int Strick = 49178;

    private const int AllEntries = 23 + 14 + 5 + 5 + 13;

    [SqlFact]
    public async Task Der_erste_Lauf_uebernimmt_alle_Themen_in_drei_Sprachen_als_offen_mit_dem_Text_der_Detailseite()
    {
        // Arrange
        await Admin();

        // Act
        var result = await Import();

        // Assert
        var strick = await Entry(Strick, SusvTopic.Restriction);
        Assert.Equal((AllEntries, AllEntries, 0, 0, 0), (result.Entries, result.New, result.Changed, result.Removed, result.FailedTopics));
        Assert.Equal(SusvReviewStatus.Pending, strick.ReviewStatus);
        Assert.Equal(["de", "fr", "it"], strick.Translations.Select(t => t.Culture).Order());
        Assert.Contains("Laichzeit der Zugersee Röteli", strick.Translations.Single(t => t.Culture == "de").Text);
        Assert.Equal("https://susv.ch/fr/arth-site-de-plongee-strick/", strick.Translations.Single(t => t.Culture == "fr").DetailUrl);
        Assert.Contains(Sql.RawData.Files.Keys, k => k.EndsWith("/susv/restriction-de.json", StringComparison.Ordinal));
        Assert.Equal(15 + 23 * 3, Sql.Sources.Requests.Count);
    }

    [SqlFact]
    public async Task Ein_zweiter_Lauf_ohne_Aenderung_holt_keine_Detailseite_und_schickt_keine_Mail()
    {
        // Arrange
        await Admin();
        await Import();
        await Sql.DeliverMail();
        var mails = Sql.Mail.Sent.Count;
        Sql.Sources.Requests.Clear();
        Sql.Time.Now = Sql.Time.Now.AddDays(1);

        // Act
        var result = await Import();
        await Sql.DeliverMail();

        // Assert
        Assert.Equal((0, 0, 0), (result.New, result.Changed, result.Removed));
        Assert.Equal(15, Sql.Sources.Requests.Count);
        Assert.Equal(mails, Sql.Mail.Sent.Count);
    }

    [SqlFact]
    public async Task Neue_Eintraege_loesen_eine_Mail_pro_Lauf_an_die_Moderation_aus()
    {
        // Arrange
        await Admin();

        // Act
        await Import();
        await Sql.DeliverMail();

        // Assert
        var mail = Assert.Single(Sql.Mail.Sent);
        Assert.Equal("admin@example.org", mail.To);
        Assert.Contains($"{AllEntries} Einträge", mail.Subject);
        Assert.Contains("/admin/susv", mail.Body);
    }

    [SqlTheory]
    [InlineData("leer")]
    [InlineData("404")]
    [InlineData("umgebaut")]
    public async Task Eine_leere_verschobene_oder_umgebaute_Antwort_entfernt_nichts(string failure)
    {
        // Arrange
        await Admin();
        await Import();
        Fail(failure);

        // Act
        var first = await Import();
        var second = await Import();

        // Assert
        Assert.True(first.FailedTopics >= 1);
        Assert.Equal((0, 0), (first.Removed, second.Removed));
        Assert.Null((await Entry(Strick, SusvTopic.Restriction)).RemovedAt);
    }

    [SqlFact]
    public async Task Fehlt_der_groesste_Teil_eines_Themas_auf_einmal_gilt_nichts_als_entfernt()
    {
        // Arrange
        await Admin();
        await Import();
        Sql.Sources.SusvMaps["restriction-de"] = Only(TestData.Read("susv", "map-restriction-de.json"), 5);

        // Act
        await Import();
        var result = await Import();

        // Assert
        Assert.Equal(0, result.Removed);
    }

    [SqlFact]
    public async Task Ein_Eintrag_gilt_erst_nach_zwei_Laeufen_ohne_ihn_als_entfernt_und_kommt_mit_seiner_Bestaetigung_zurueck()
    {
        // Arrange
        var admin = await Admin();
        var site = await Site(admin, "Strick", 47.0662, 8.5245);
        await Import();
        var entry = await Entry(Strick, SusvTopic.Restriction);
        await Review(admin, entry.Id, SusvReviewAction.Confirm, [site], NoticeKind.Closure);
        Sql.Sources.SusvMaps["restriction-de"] = Without(TestData.Read("susv", "map-restriction-de.json"), Strick);

        // Act
        var first = await Import();
        var afterFirst = await Entry(Strick, SusvTopic.Restriction);
        var second = await Import();
        var afterSecond = await Entry(Strick, SusvTopic.Restriction);
        Sql.Sources.SusvMaps.Clear();
        await Import();
        var back = await Entry(Strick, SusvTopic.Restriction);

        // Assert
        Assert.Equal((0, 1), (first.Removed, second.Removed));
        Assert.Null(afterFirst.RemovedAt);
        Assert.NotNull(afterSecond.RemovedAt);
        Assert.Null(back.RemovedAt);
        Assert.Equal((SusvReviewStatus.Confirmed, NoticeKind.Closure), (back.ReviewStatus, back.NoticeKind));
        Assert.Equal([site], back.Sites.Select(s => s.DiveSiteId));
    }

    [SqlFact]
    public async Task Ein_Eintrag_der_nach_ueber_30_Tagen_zurueckkommt_behaelt_seine_Bestaetigung_statt_geloescht_zu_werden()
    {
        // Arrange
        var admin = await Admin();
        var site = await Site(admin, "Strick", 47.0662, 8.5245);
        await Import();
        var entry = await Entry(Strick, SusvTopic.Restriction);
        await Review(admin, entry.Id, SusvReviewAction.Confirm, [site], NoticeKind.Closure);
        Sql.Sources.SusvMaps["restriction-de"] = Without(TestData.Read("susv", "map-restriction-de.json"), Strick);
        await Import();
        await Import();
        Sql.Sources.SusvMaps.Clear();
        Sql.Time.Now = Sql.Time.Now.AddDays(31);

        // Act
        await Import();
        var back = await Entry(Strick, SusvTopic.Restriction);

        // Assert
        Assert.Null(back.RemovedAt);
        Assert.Equal((SusvReviewStatus.Confirmed, entry.Id), (back.ReviewStatus, back.Id));
        Assert.Equal([site], back.Sites.Select(s => s.DiveSiteId));
    }

    [SqlFact]
    public async Task Aendert_der_SUSV_einen_bestaetigten_Eintrag_bleiben_Plaetze_und_Art_und_die_Moderation_sieht_alt_und_neu()
    {
        // Arrange
        var admin = await Admin();
        var site = await Site(admin, "Strick", 47.0662, 8.5245);
        await Import();
        var entry = await Entry(Strick, SusvTopic.Restriction);
        await Review(admin, entry.Id, SusvReviewAction.Confirm, [site], NoticeKind.Closure);
        Sql.Sources.SusvMaps["restriction-de"] = TestData.Read("susv", "map-restriction-de.json").Replace("Arth, Tauchplatz Strick", "Arth, Strick wieder offen");

        // Act
        var result = await Import();
        var list = await Query<GetSusvEntriesForReviewQuery, SusvReviewList?>(new GetSusvEntriesForReviewQuery(admin, "de"));

        // Assert
        var item = list!.Entries.Single(e => e.Id == entry.Id);
        Assert.Equal(1, result.Changed);
        Assert.Equal((true, "Arth, Strick wieder offen", "Arth, Tauchplatz Strick"), (item.ChangedSinceReview, item.Title, item.PreviousTitle));
        Assert.Equal(NoticeKind.Closure, item.Kind);
        Assert.Equal([site], item.SiteIds);
        Assert.Equal(list.Entries.Count(e => e.Status == SusvReviewStatus.Pending), list.Entries.ToList().IndexOf(item));
    }

    [SqlFact]
    public async Task Haelt_ein_anderer_Lauf_den_Lock_geschieht_nichts()
    {
        // Arrange
        await Admin();
        await using var held = await Sql.RunLock.TryAcquire(ImportSusvEntriesHandler.LockName, TestContext.Current.CancellationToken);

        // Act
        var result = await Import();

        // Assert
        Assert.True(result.Locked);
        Assert.Empty(Sql.Sources.Requests);
    }

    [SqlFact]
    public async Task Das_Echolot_zeigt_den_Abgleich_ab_36_Stunden_als_verspaetet()
    {
        // Arrange
        await Admin();
        await Import();
        Sql.Time.Now = Sql.Time.Now.AddHours(37);

        // Act
        var echolot = await Query<GetEcholotQuery, EcholotReport>(new GetEcholotQuery("de"));

        // Assert
        Assert.Equal(DataHealth.Late, echolot.Sources.Single(s => s.Source == DataSource.Susv).Health);
    }

    private Task<ImportSusvEntriesResult> Import() => Command<ImportSusvEntriesCommand, ImportSusvEntriesResult>(new ImportSusvEntriesCommand());

    private Task<ReviewSusvEntryResult> Review(string reviewer, Guid entry, SusvReviewAction action, IReadOnlyList<Guid>? sites = null, NoticeKind? kind = null) =>
        Command<ReviewSusvEntryCommand, ReviewSusvEntryResult>(new ReviewSusvEntryCommand(reviewer, entry, action, sites, kind));

    private void Fail(string failure)
    {
        switch (failure)
        {
            case "leer":
                Sql.Sources.SusvMaps["restriction-de"] = """{"content":{"entries":[],"groups":[]}}""";
                break;
            case "404":
                Sql.Sources.Statuses["/de/api/v1/map-result"] = HttpStatusCode.NotFound;
                break;
            default:
                Sql.Sources.SusvMaps["restriction-de"] = """{"data":{"pins":[]}}""";
                break;
        }
    }

    private async Task<Data.Entities.SusvEntry> Entry(int sourceId, SusvTopic topic)
    {
        await using var scope = Sql.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>().SusvEntries.AsNoTracking()
            .Include(e => e.Translations).Include(e => e.Sites)
            .SingleAsync(e => e.SourceId == sourceId && e.Topic == topic, TestContext.Current.CancellationToken);
    }

    private async Task<string> Admin()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        return (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
    }

    private async Task<Guid> Site(string admin, string name, double latitude, double longitude)
    {
        var lake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).Single(w => w.Slug == "zugersee").Id;
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(admin, name, lake, latitude, longitude, "Arth", "SZ", null));
        return result.DiveSiteId!.Value;
    }

    private static string Without(string json, int id)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var entries = node["content"]!["entries"]!.AsArray();
        entries.Remove(entries.Single(e => (int)e!["id"]! == id));
        return node.ToJsonString();
    }

    private static string Only(string json, int count)
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(json)!;
        var entries = node["content"]!["entries"]!.AsArray();
        while (entries.Count > count)
        {
            entries.RemoveAt(entries.Count - 1);
        }

        return node.ToJsonString();
    }
}
