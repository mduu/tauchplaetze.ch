using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Editorial;

/// <remarks>Ausgangslage: Riff Williams aus dem Erstimport (Version 1), ein Admin als Moderation und eine Mitwirkende «Anna».</remarks>
public class EditorialTests(SqlServer sql) : SqlTest(sql)
{
    private const string Base = "https://localhost/";

    [SqlFact]
    public async Task Ein_Vorschlag_von_Mitwirkenden_ist_erst_nach_der_Freigabe_online_und_die_Autorin_bekommt_eine_Mail()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        var proposed = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 32 }, "Tiefe ergänzt"));
        var beforeApproval = await PublicMaxDepth(site);
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id, Base));

        // Assert
        Assert.Equal((2, RevisionStatus.Pending), (proposed.Number, proposed.Status));
        Assert.Null(beforeApproval);
        Assert.Equal(32, await PublicMaxDepth(site));
        Assert.Contains(Sql.Mail.Sent, m => m.To == "anna@example.org" && m.Body.Contains("https://localhost/de/site/riff-williams-herrliberg", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Eine_Aenderung_der_Moderation_ist_sofort_online_ohne_Mail()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { MaxDepth = 30 }, null));

        // Assert
        Assert.Equal(RevisionStatus.Approved, result.Status);
        Assert.Equal(30, await PublicMaxDepth(site));
        Assert.DoesNotContain(Sql.Mail.Sent, m => m.Subject.Contains("online", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Ohne_Aenderung_entsteht_keine_neue_Version()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content, null));

        // Assert
        Assert.True(result.NoChange);
        Assert.Single(await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site)));
    }

    [SqlFact]
    public async Task Mitwirkende_aendern_weder_Slug_noch_Prognose_ein_Moderator_schon_und_der_alte_Slug_leitet_weiter()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { Slug = "anderer-slug", ForecastDisabled = true, MaxDepth = 25 }, null));
        var proposedContent = await Changes(site, 1, 2);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { Slug = "riff-williams" }, null));
        var lookup = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "de"));

        // Assert
        Assert.Equal(["MaxDepth"], proposedContent.Select(c => c.Field));
        Assert.Equal(new DiveSiteLookup.Moved("riff-williams"), lookup);
    }

    [SqlFact]
    public async Task Eine_Ablehnung_hat_eine_Begruendung_per_Mail_und_nur_die_Autorin_kann_zurueckziehen()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 99 }, null));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 33 }, null));
        var pending = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery());

        // Act
        var rejected = await Command<RejectRevisionCommand, EditorialResult>(new RejectRevisionCommand(moderator, pending[0].Revision.Id, "99 m stimmt nicht."));
        var withdrawnByOther = await Command<WithdrawRevisionCommand, EditorialResult>(new WithdrawRevisionCommand(moderator, pending[1].Revision.Id));
        var withdrawn = await Command<WithdrawRevisionCommand, EditorialResult>(new WithdrawRevisionCommand(anna, pending[1].Revision.Id));

        // Assert
        Assert.Equal(RevisionStatus.Rejected, rejected.Status);
        Assert.Contains(Sql.Mail.Sent, m => m.To == "anna@example.org" && m.Body.Contains("99 m stimmt nicht.", StringComparison.Ordinal));
        Assert.Equal([EditorialError.NotFound], withdrawnByOther.Errors);
        Assert.Equal(RevisionStatus.Withdrawn, withdrawn.Status);
        Assert.Empty(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));
    }

    [SqlFact]
    public async Task Mitwirkende_koennen_nicht_freigeben()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 20 }, null));
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));

        // Act
        var result = await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(anna, pending.Revision.Id, Base));

        // Assert
        Assert.Equal([EditorialError.NotAllowed], result.Errors);
    }

    [SqlFact]
    public async Task Wiederherstellen_erzeugt_eine_neue_freigegebene_Version_mit_dem_alten_Inhalt()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { MaxDepth = 40 }, null));

        // Act
        var restored = await Command<RestoreRevisionCommand, EditorialResult>(new RestoreRevisionCommand(moderator, site, 1));

        // Assert
        Assert.Equal((3, RevisionStatus.Approved), (restored.Number, restored.Status));
        Assert.Null(await PublicMaxDepth(site));
        Assert.Empty(await Changes(site, 1, 3));
    }

    [SqlFact]
    public async Task Ein_neuer_Platz_von_Mitwirkenden_ist_Entwurf_bis_zur_Freigabe()
    {
        // Arrange
        var (moderator, anna, _) = await Prepare();
        var waterBody = (await ForEditing((await Query<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>>(new GetDiveSitesForEditingQuery()))[0].Id)).Content.WaterBodyId;

        // Act
        var proposed = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(anna, "Steinbock", waterBody, 47.30, 8.58, "Erlenbach", "ZH", "Neuer Platz"));
        var beforeApproval = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("steinbock-erlenbach", "de"));
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id, Base));
        var afterApproval = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("steinbock-erlenbach", "de"));

        // Assert
        Assert.Equal(RevisionStatus.Pending, proposed.Status);
        Assert.IsType<DiveSiteLookup.NotFound>(beforeApproval);
        Assert.IsType<DiveSiteLookup.Found>(afterApproval);
    }

    [SqlFact]
    public async Task Ein_Platz_ausserhalb_der_Schweiz_wird_abgelehnt()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var waterBody = (await ForEditing(site)).Content.WaterBodyId;

        // Act
        var result = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(anna, "Gardasee", waterBody, 45.6, 10.7, null, null, null));

        // Assert
        Assert.Equal([EditorialError.LocationOutsideSwitzerland], result.Errors);
    }

    [SqlFact]
    public async Task Die_oeffentliche_History_zeigt_nur_freigegebene_Versionen_und_Namen_nur_mit_Einwilligung()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 31 }, null));
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id, Base));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, 2, edit.Content with { MaxDepth = 50 }, null));

        // Act
        var anonymous = await Query<GetPublicRevisionsQuery, PublicRevisions?>(new GetPublicRevisionsQuery("riff-williams-herrliberg"));
        await Command<UpdateProfileCommand, AccountResult>(new UpdateProfileCommand(anna, "Anna", true, "de"));
        var withConsent = await Query<GetPublicRevisionsQuery, PublicRevisions?>(new GetPublicRevisionsQuery("riff-williams-herrliberg"));

        // Assert
        Assert.Equal([2, 1], anonymous!.Revisions.Select(r => r.Number));
        Assert.All(anonymous.Revisions, r => Assert.Null(r.AuthorName));
        Assert.Equal("Anna", withConsent!.Revisions[0].AuthorName);
        Assert.Null(withConsent.Revisions[0].ReviewerName);
    }

    [SqlFact]
    public async Task Wird_ein_Konto_gesperrt_werden_seine_offenen_Vorschlaege_abgelehnt_und_es_kann_nichts_mehr_vorschlagen()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 21 }, null));

        // Act
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(moderator, anna, true));
        var afterBlock = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 22 }, null));

        // Assert
        Assert.Empty(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery()));
        Assert.Equal([EditorialError.Blocked], afterBlock.Errors);
    }

    private async Task<(string Moderator, string Anna, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        await Command<ImportDiveSitesCommand, ImportDiveSitesResult>(new ImportDiveSitesCommand(moderator));
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("anna@example.org", "Felswand-auf-25m", "Anna", "de", "https://localhost/c?user={user}&token={token}"));
        var link = new Uri(Sql.Mail.LinkTo("anna@example.org"));
        var query = link.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        await Command<ConfirmEmailCommand, AccountResult>(new ConfirmEmailCommand(query["user"], query["token"]));
        Sql.Mail.Sent.Clear();
        var site = (await Query<GetDiveSitesForEditingQuery, IReadOnlyList<EditableDiveSite>>(new GetDiveSitesForEditingQuery())).Single().Id;
        return (moderator, query["user"], site);
    }

    private async Task<DiveSiteForEditing> ForEditing(Guid site) =>
        (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

    private async Task<int?> PublicMaxDepth(Guid site) => (await ForEditing(site)).Content.MaxDepth;

    private async Task<IReadOnlyList<FieldChange>> Changes(Guid site, int from, int to) =>
        (await Query<CompareRevisionsQuery, IReadOnlyList<FieldChange>?>(new CompareRevisionsQuery(site, from, to)))!;
}
