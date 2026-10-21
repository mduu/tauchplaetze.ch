using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.Reports;
using Tauchplaetze.Core.Features.Reports.Commands;
using Tauchplaetze.Core.Features.Reports.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Reports;

public class ReportTests(SqlServer sql) : SqlTest(sql)
{
    private static readonly DateOnly Today = new(2026, 10, 17);

    [SqlTheory]
    [InlineData(0, 6.5, null, null, 0, null)]
    [InlineData(-7, 6.5, 14.2, 10, 0, null)]
    [InlineData(-8, 6.5, null, null, 0, ReportError.InvalidDate)]
    [InlineData(1, 6.5, null, null, 0, ReportError.InvalidDate)]
    [InlineData(0, 0.25, null, null, 0, ReportError.InvalidVisibility)]
    [InlineData(0, 30.5, null, null, 0, ReportError.InvalidVisibility)]
    [InlineData(0, 6.5, 31.0, null, 0, ReportError.InvalidTemperature)]
    [InlineData(0, 6.5, 12.25, null, 0, ReportError.InvalidTemperature)]
    [InlineData(0, 6.5, null, 10, 0, ReportError.InvalidDepth)]
    [InlineData(0, 6.5, 12.0, 101, 0, ReportError.InvalidDepth)]
    [InlineData(0, 6.5, null, null, 281, ReportError.CommentTooLong)]
    public async Task Eine_Meldung_gilt_fuer_heute_bis_7_Tage_zurueck_mit_Sicht_0_5_bis_30_m_Temperatur_minus_2_bis_30_Grad_und_kurzem_Kommentar(
        int daysFromToday, double visibility, double? temperature, int? depth, int commentLength, ReportError? expected)
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var comment = commentLength > 0 ? new string('x', commentLength) : null;

        // Act
        var result = await Submit(anna, site, Today.AddDays(daysFromToday), (decimal)visibility, (decimal?)temperature, depth, comment);

        // Assert
        Assert.Equal(expected is null ? [] : [expected.Value], result.Errors);
    }

    [SqlFact]
    public async Task Nur_ein_bestaetigtes_nicht_gesperrtes_Konto_meldet()
    {
        // Arrange
        var (admin, anna, site) = await Prepare();
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("bert@example.org", "Felswand-auf-25m", "Bert", "de", "/c?user={user}&token={token}"));
        var bert = (await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(admin))).Single(a => a.DisplayName == "Bert").UserId;
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));

        // Act
        var unconfirmed = await Submit(bert, site, Today, 6.5m);
        var blocked = await Submit(anna, site, Today, 6.5m);
        var unknown = await Submit("unbekannt", site, Today, 6.5m);

        // Assert
        Assert.Equal([ReportError.NotAllowed], unconfirmed.Errors);
        Assert.Equal([ReportError.NotAllowed], blocked.Errors);
        Assert.Equal([ReportError.NotAllowed], unknown.Errors);
        Assert.Empty(await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site)));
    }

    [SqlFact]
    public async Task Eine_zweite_Meldung_fuer_denselben_Platz_und_Tag_ersetzt_die_eigene()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var first = await Submit(anna, site, Today, 4m);

        // Act
        var second = await Submit(anna, site, Today, 6.5m, 12.4m, 15);
        var reports = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site));

        // Assert
        Assert.Equal(first.ReportId, second.ReportId);
        Assert.Equal((6.5m, (decimal?)12.4m, (int?)15), (reports.Single().Visibility, reports.Single().Temperature, reports.Single().TemperatureDepth));
    }

    [SqlFact]
    public async Task Ein_Konto_meldet_hoechstens_10_neue_Tauchgaenge_pro_Tag()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        for (var day = 0; day <= ReportRules.MaxDaysBack; day++)
        {
            await Submit(anna, site, Today.AddDays(-day), 5m);
        }

        var otherSite = await AddSecondSite();
        await Submit(anna, otherSite, Today, 5m);
        await Submit(anna, otherSite, Today.AddDays(-1), 5m);

        // Act
        var eleventh = await Submit(anna, otherSite, Today.AddDays(-2), 5m);
        var replaced = await Submit(anna, site, Today, 7m);

        // Assert
        Assert.Equal([ReportError.DailyLimit], eleventh.Errors);
        Assert.True(replaced.Succeeded);
    }

    [SqlFact]
    public async Task Ein_Kommentar_ist_erst_nach_Freigabe_oeffentlich_vorher_sehen_ihn_nur_Autor_und_Moderation()
    {
        // Arrange
        var (admin, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        var report = (await Submit(anna, site, Today, 6m, comment: "Unter 15 m sehr klar")).ReportId!.Value;

        // Act
        var before = await Comments(site, null, anna, admin);
        var byBert = await Command<ModerateReportCommand, ReportResult>(new ModerateReportCommand(bert, report, ReportModeration.ApproveComment));
        await Command<ModerateReportCommand, ReportResult>(new ModerateReportCommand(admin, report, ReportModeration.ApproveComment));
        var approved = await Comments(site, null, anna, admin);
        await Submit(anna, site, Today, 6m, comment: "Unter 15 m sehr klar, oben trüb");
        var changed = await Comments(site, null, anna, admin);

        // Assert
        Assert.Equal([null, "Unter 15 m sehr klar", "Unter 15 m sehr klar"], before);
        Assert.Equal([ReportError.NotAllowed], byBert.Errors);
        Assert.Equal(["Unter 15 m sehr klar", "Unter 15 m sehr klar", "Unter 15 m sehr klar"], approved);
        Assert.Equal([null, "Unter 15 m sehr klar, oben trüb", "Unter 15 m sehr klar, oben trüb"], changed);
    }

    [SqlFact]
    public async Task Eine_ausgeblendete_Meldung_fehlt_oeffentlich_ein_abgelehnter_Kommentar_ist_weg_die_Zahlen_bleiben()
    {
        // Arrange
        var (admin, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        var spam = (await Submit(bert, site, Today, 30m, comment: "Billige Uhren")).ReportId!.Value;
        var real = (await Submit(anna, site, Today, 6m, comment: "Kalt")).ReportId!.Value;

        // Act
        await Command<ModerateReportCommand, ReportResult>(new ModerateReportCommand(admin, spam, ReportModeration.Hide));
        await Command<ModerateReportCommand, ReportResult>(new ModerateReportCommand(admin, real, ReportModeration.RejectComment));
        var reports = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site, admin));
        var queue = await Query<GetReportQueueQuery, ReportQueue?>(new GetReportQueueQuery(admin));
        var queueForAnna = await Query<GetReportQueueQuery, ReportQueue?>(new GetReportQueueQuery(anna));

        // Assert
        Assert.Equal((6m, (string?)null), (reports.Single().Visibility, reports.Single().Comment));
        Assert.Empty(queue!.PendingComments);
        Assert.Contains(queue.Recent, r => r.Id == spam && r.IsHidden);
        Assert.Null(queueForAnna);
    }

    [SqlFact]
    public async Task Der_Name_erscheint_nur_mit_Einwilligung()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        await Command<UpdateProfileCommand, AccountResult>(new UpdateProfileCommand(anna, "Anna", true, "de"));
        await Submit(anna, site, Today, 6m);
        await Submit(bert, site, Today.AddDays(-1), 5m);

        // Act
        var reports = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site));

        // Assert
        Assert.Equal(["Anna", null], reports.Select(r => r.AuthorName));
    }

    [SqlFact]
    public async Task Nach_der_Loeschung_des_Kontos_bleibt_die_Meldung_ohne_Autor_und_ohne_Kommentar()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        await Command<UpdateProfileCommand, AccountResult>(new UpdateProfileCommand(anna, "Anna", true, "de"));
        await Submit(anna, site, Today, 6m, 12m, 20, "Sehr klar");
        var personal = await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(anna));

        // Act
        await Command<DeleteAccountCommand, AccountResult>(new DeleteAccountCommand(anna));
        var reports = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site));

        // Assert
        Assert.Equal("Sehr klar", personal!.ReportList.Single().Comment);
        Assert.Equal((6m, (string?)null, (string?)null, ReportCommentStatus.None), (reports.Single().Visibility, reports.Single().AuthorName, reports.Single().Comment, reports.Single().CommentStatus));
    }

    [SqlFact]
    public async Task Die_Platzseite_zeigt_die_Meldungen_der_letzten_14_Tage()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        await Submit(anna, site, Today.AddDays(-7), 5m);

        // Act
        Sql.Time.Now = FixedTime.SpikeDay.AddDays(6);
        var lastDay = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site));
        Sql.Time.Now = FixedTime.SpikeDay.AddDays(7);
        var dayAfter = await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site));

        // Assert
        Assert.Single(lastDay);
        Assert.Empty(dayAfter);
    }

    private async Task<string?[]> Comments(Guid site, params string?[] viewers)
    {
        var comments = new List<string?>();
        foreach (var viewer in viewers)
        {
            comments.Add((await Query<GetSiteReportsQuery, IReadOnlyList<SiteReport>>(new GetSiteReportsQuery(site, viewer))).Single().Comment);
        }

        return [.. comments];
    }

    private Task<ReportResult> Submit(string user, Guid site, DateOnly date, decimal visibility, decimal? temperature = null, int? depth = null, string? comment = null) =>
        Command<SubmitReportCommand, ReportResult>(new SubmitReportCommand(user, site, date, visibility, temperature, depth, comment));

    private async Task<(string Admin, string Anna, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var site = await AddRiffWilliams(admin);
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        return (admin, anna, site);
    }

    private async Task<Guid> AddSecondSite()
    {
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var waterBodies = await Query<Core.Features.WaterBodies.Queries.GetWaterBodiesQuery, IReadOnlyList<Core.Features.WaterBodies.WaterBodySummary>>(
            new Core.Features.WaterBodies.Queries.GetWaterBodiesQuery("de"));
        var lake = waterBodies.Single(w => w.Slug == "zuerichsee").Id;
        var result = await Command<Core.Features.Editorial.Commands.ProposeDiveSiteCommand, Core.Features.Editorial.EditorialResult>(
            new Core.Features.Editorial.Commands.ProposeDiveSiteCommand(admin, "Seehalde", lake, 47.30, 8.58, "Meilen", "ZH", null));
        return result.DiveSiteId!.Value;
    }
}
