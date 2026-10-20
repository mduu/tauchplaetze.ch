using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.DiveSites.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.WaterBodies;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Editorial;

/// <remarks>Ausgangslage: Riff Williams aus dem Erstimport (Version 1), ein Admin als Moderation und eine Mitwirkende «Anna».</remarks>
public class EditorialTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_Vorschlag_von_Mitwirkenden_ist_erst_nach_der_Freigabe_online_und_die_Autorin_bekommt_eine_Mail()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        var proposed = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 32 }, "Tiefe ergänzt"));
        var beforeApproval = await PublicMaxDepth(site);
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id));

        // Assert
        Assert.Equal((2, RevisionStatus.Pending), (proposed.Number, proposed.Status));
        Assert.Null(beforeApproval);
        Assert.Equal(32, await PublicMaxDepth(site));
        await Sql.DeliverMail();
        Assert.Contains(Sql.Mail.Sent, m => m.To == "anna@example.org" && m.Body.Contains("https://tauchplaetze.test/de/site/riff-williams-herrliberg", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Mitwirkende_sehen_ihre_Vorschlaege_mit_Status_und_Begruendung_eigene_Freigaben_der_Moderation_zaehlen_nicht()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 99 }, "Tiefe"));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 33 }, null));
        var pending = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator));
        await Command<RejectRevisionCommand, EditorialResult>(new RejectRevisionCommand(moderator, pending.Single(p => p.Revision.Number == 2).Revision.Id, "99 m stimmt nicht."));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { MaxDepth = 20 }, null));

        // Act
        var all = await Query<GetMyProposalsQuery, IReadOnlyList<MyProposal>>(new GetMyProposalsQuery(anna));
        var openHere = await Query<GetMyProposalsQuery, IReadOnlyList<MyProposal>>(new GetMyProposalsQuery(anna, site, PendingOnly: true));
        var ofModerator = await Query<GetMyProposalsQuery, IReadOnlyList<MyProposal>>(new GetMyProposalsQuery(moderator));

        // Assert
        Assert.Equal([(3, RevisionStatus.Pending, (string?)null), (2, RevisionStatus.Rejected, "99 m stimmt nicht.")], all.Select(p => (p.Number, p.Status, p.ReviewComment)));
        Assert.Equal([3], openHere.Select(p => p.Number));
        Assert.Empty(ofModerator);
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
        await Sql.DeliverMail();
        Assert.DoesNotContain(Sql.Mail.Sent, m => m.Subject.Contains("online", StringComparison.Ordinal));
    }

    [SqlFact]
    public async Task Mitwirkende_sehen_im_CMS_weder_die_Versionen_noch_den_Vergleich_fremder_Vorschlaege()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        var bert = await Confirmed("bert@example.org", "Bert");
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(bert, site, edit.BaseNumber, edit.Content with { MaxDepth = 33 }, "geheim"));

        // Act
        var versions = await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, anna));
        var compare = await Query<CompareRevisionsQuery, IReadOnlyList<FieldChange>?>(new CompareRevisionsQuery(site, 1, 2, anna));
        var forModeration = await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, moderator));

        // Assert
        Assert.Empty(versions);
        Assert.Null(compare);
        Assert.Equal(2, forModeration.Count);
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
        Assert.Single(await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, await Moderator())));
    }

    [SqlFact]
    public async Task Mitwirkende_aendern_weder_Slug_noch_Gewaesser_noch_Prognose_noch_Abfluss_Schwellen_ein_Moderator_schon_und_der_alte_Slug_leitet_weiter()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        var otherLake = (await Query<GetWaterBodiesQuery, IReadOnlyList<WaterBodySummary>>(new GetWaterBodiesQuery("de"))).First(w => w.Id != edit.Content.WaterBodyId).Id;

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber,
            edit.Content with { Slug = "anderer-slug", WaterBodyId = otherLake, ForecastDisabled = true, Discharge = new DischargeThresholds(100, 300, 600), MaxDepth = 25 }, null));
        var proposedContent = await Changes(site, 1, 2);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { Slug = "riff-williams" }, null));
        var lookup = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "de"));

        // Assert
        Assert.Equal(["MaxDepth"], proposedContent.Select(c => c.Field));
        Assert.Equal(new DiveSiteLookup.Moved("riff-williams"), lookup);
    }

    [SqlFact]
    public async Task Wer_einen_automatisch_uebersetzten_Text_aendert_nimmt_den_Hinweis_weg_ein_unveraenderter_behaelt_ihn()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var first = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, first.BaseNumber, first.Content with
        {
            Translations =
            [
                new DiveSiteContentTranslation("fr", "Paroi jusqu'à 30 m.", null, null, null, null, null, true),
                new DiveSiteContentTranslation("it", "Parete fino a 30 m.", null, null, null, null, null, true),
            ],
        }, null));
        var second = await ForEditing(site);

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, second.BaseNumber, second.Content with
        {
            Translations =
            [
                new DiveSiteContentTranslation("fr", "Paroi raide jusqu'à 30 m.", null, null, null, null, null, true),
                new DiveSiteContentTranslation("it", "Parete fino a 30 m.", null, null, null, null, null, true),
            ],
        }, null));

        // Assert
        var after = (await ForEditing(site)).Content;
        Assert.False(after.TextIn("fr")!.IsMachineTranslated);
        Assert.True(after.TextIn("it")!.IsMachineTranslated);
    }

    [SqlFact]
    public async Task Eine_Ablehnung_hat_eine_Begruendung_per_Mail_und_nur_die_Autorin_kann_zurueckziehen()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 99 }, null));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 33 }, null));
        var pending = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator));

        // Act
        var rejected = await Command<RejectRevisionCommand, EditorialResult>(new RejectRevisionCommand(moderator, pending[0].Revision.Id, "99 m stimmt nicht."));
        var withdrawnByOther = await Command<WithdrawRevisionCommand, EditorialResult>(new WithdrawRevisionCommand(moderator, pending[1].Revision.Id));
        var withdrawn = await Command<WithdrawRevisionCommand, EditorialResult>(new WithdrawRevisionCommand(anna, pending[1].Revision.Id));

        // Assert
        Assert.Equal(RevisionStatus.Rejected, rejected.Status);
        await Sql.DeliverMail();
        Assert.Contains(Sql.Mail.Sent, m => m.To == "anna@example.org" && m.Body.Contains("99 m stimmt nicht.", StringComparison.Ordinal));
        Assert.Equal([EditorialError.NotFound], withdrawnByOther.Errors);
        Assert.Equal(RevisionStatus.Withdrawn, withdrawn.Status);
        Assert.Empty(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));
    }

    [SqlFact]
    public async Task Mitwirkende_koennen_nicht_freigeben()
    {
        // Arrange
        var (moderator, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 20 }, null));
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));

        // Act
        var result = await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(anna, pending.Revision.Id));

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
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id));
        var afterApproval = await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("steinbock-erlenbach", "de"));

        // Assert
        Assert.Equal(RevisionStatus.Pending, proposed.Status);
        Assert.IsType<DiveSiteLookup.NotFound>(beforeApproval);
        Assert.IsType<DiveSiteLookup.Found>(afterApproval);
    }

    [SqlFact]
    public async Task Ein_Platz_bekommt_nie_den_Slug_der_Seite_Neuer_Platz()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        var proposed = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(moderator, "Nuovo", edit.Content.WaterBodyId, 47.30, 8.58, null, null, null));
        var renamed = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { Slug = "neu" }, null));

        // Assert
        Assert.Equal("nuovo-2", (await ForEditing(proposed.DiveSiteId!.Value)).Content.Slug);
        Assert.Equal([EditorialError.InvalidSlug], renamed.Errors);
    }

    [SqlFact]
    public async Task Ein_neuer_Platz_braucht_eine_Koordinate_und_zeigt_vorher_die_Plaetze_im_Umkreis_von_300_m()
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var content = (await ForEditing(site)).Content;

        // Act
        var missing = await Command<ProposeDiveSiteCommand, EditorialResult>(new ProposeDiveSiteCommand(anna, "Steinbock", content.WaterBodyId, null, null, null, null, null));
        var near = await Query<GetNearbyDiveSitesQuery, IReadOnlyList<NearbyDiveSite>>(new GetNearbyDiveSitesQuery(content.Latitude + 0.002, content.Longitude));
        var far = await Query<GetNearbyDiveSitesQuery, IReadOnlyList<NearbyDiveSite>>(new GetNearbyDiveSitesQuery(content.Latitude + 0.004, content.Longitude));

        // Assert
        Assert.Equal([EditorialError.MissingLocation], missing.Errors);
        var found = Assert.Single(near);
        Assert.Equal(content.Slug, found.Slug);
        Assert.InRange(found.DistanceMeters, 215, 230);
        Assert.Empty(far);
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
        var pending = Assert.Single(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(moderator, pending.Revision.Id));
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
        Assert.Empty(await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(moderator)));
        Assert.Equal([EditorialError.Blocked], afterBlock.Errors);
    }

    [SqlFact]
    public async Task Ein_Admin_Konto_sperren_nur_Admins()
    {
        // Arrange
        var (admin, anna, _) = await Prepare();
        var bert = await Moderator(admin);
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, anna, Roles.Admin, true));

        // Act
        var byModerator = await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(bert, anna, true));
        var byAdmin = await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));

        // Assert
        Assert.Equal([AccountError.NotAllowed], byModerator.Errors);
        Assert.True(byAdmin.Succeeded);
    }

    [SqlFact]
    public async Task Ein_gesperrtes_Konto_aendert_sofort_nichts_mehr_kann_seine_Daten_aber_weiter_lesen()
    {
        // Arrange
        var (admin, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, anna, Roles.Admin, true));
        var proposal = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 21 }, null));
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));
        var revision = (await Query<GetRevisionsQuery, IReadOnlyList<RevisionSummary>>(new GetRevisionsQuery(site, await Moderator()))).Single(r => r.Number == proposal.Number);

        // Act
        var profile = await Command<UpdateProfileCommand, AccountResult>(new UpdateProfileCommand(anna, "Anna B.", true, "fr"));
        var withdraw = await Command<WithdrawRevisionCommand, EditorialResult>(new WithdrawRevisionCommand(anna, revision.Id));
        var role = await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(anna, admin, Roles.Moderator, false));
        var archive = await Command<ArchiveDiveSiteCommand, EditorialResult>(new ArchiveDiveSiteCommand(anna, site, true));
        var data = await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(anna));

        // Assert
        Assert.Equal([AccountError.Blocked], profile.Errors);
        Assert.Equal([EditorialError.Blocked], withdraw.Errors);
        Assert.Equal([AccountError.Blocked], role.Errors);
        Assert.Equal([EditorialError.Blocked], archive.Errors);
        Assert.Equal("Anna", data!.DisplayName);
        Assert.NotNull(data.BlockedAt);
    }

    [SqlFact]
    public async Task Der_letzte_aktive_Admin_loescht_sich_nicht_ein_gesperrter_Admin_zaehlt_nicht()
    {
        // Arrange
        var (admin, anna, _) = await Prepare();
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, anna, Roles.Admin, true));
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));

        // Act
        var whileOtherBlocked = await Command<DeleteAccountCommand, AccountResult>(new DeleteAccountCommand(admin));
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, false));
        var whileOtherActive = await Command<DeleteAccountCommand, AccountResult>(new DeleteAccountCommand(admin));

        // Assert
        Assert.Equal([AccountError.LastAdmin], whileOtherBlocked.Errors);
        Assert.True(whileOtherActive.Succeeded);
    }

    [SqlFact]
    public async Task Mitwirkende_sehen_weder_Konten_noch_offene_Vorschlaege_und_Moderatoren_keine_E_Mail_Adressen()
    {
        // Arrange
        var (admin, anna, site) = await Prepare();
        var bert = await Moderator(admin);
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 21 }, null));

        // Act
        var pendingForAnna = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(anna));
        var pendingForBert = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(bert));
        var accountsForAnna = await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(anna));
        var accountsForBert = await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(bert));
        var accountsForAdmin = await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(admin));

        // Assert
        Assert.Empty(pendingForAnna);
        Assert.Single(pendingForBert);
        Assert.Empty(accountsForAnna);
        Assert.Equal(3, accountsForBert.Count);
        Assert.All(accountsForBert, a => Assert.Equal("", a.Email));
        Assert.Contains(accountsForAdmin, a => a.Email == "anna@example.org");
    }

    [SqlFact]
    public async Task Hinweise_gelten_nur_in_ihrem_Zeitraum_der_schwerste_zuerst_und_ohne_Uebersetzung_auf_Deutsch()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);
        NoticeContent[] notices =
        [
            Notice(NoticeKind.Info, null, null, ("de", "Parkplatz kostet"), ("fr", "Parking payant")),
            Notice(NoticeKind.Closure, null, new DateOnly(2026, 10, 17), ("de", "Gesperrt bis heute")),
            Notice(NoticeKind.Warning, new DateOnly(2026, 10, 18), null, ("de", "Ab morgen Bauarbeiten")),
            Notice(NoticeKind.Info, null, new DateOnly(2026, 10, 16), ("de", "Gestern vorbei")),
        ];

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { Notices = notices }, null));
        var details = ((DiveSiteLookup.Found)await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery("riff-williams-herrliberg", "fr"))).Site;
        var listed = Assert.Single(await Query<GetDiveSitesQuery, IReadOnlyList<DiveSiteListItem>>(new GetDiveSitesQuery("fr")));

        // Assert
        Assert.Equal(
            [(NoticeKind.Closure, "Gesperrt bis heute", true), (NoticeKind.Info, "Parking payant", false)],
            details.Notices.Select(n => (n.Kind, n.Text, n.GermanOnly)));
        Assert.Equal(NoticeKind.Closure, listed.NoticeToday);
    }

    [SqlFact]
    public async Task Ein_vorhandener_Hinweis_laesst_sich_aendern_und_entfernen()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var first = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, first.BaseNumber,
            first.Content with { Notices = [Notice(NoticeKind.Warning, null, null, ("de", "Parkplatz zu"), ("fr", "Parking fermé"))] }, null));
        var second = await ForEditing(site);
        var saved = Assert.Single(second.Content.NoticeList);

        // Act
        var changed = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, second.BaseNumber,
            second.Content with { Notices = [saved with { Kind = NoticeKind.Closure, Texts = [new NoticeText("de", "Gesperrt")] }] }, null));
        var afterChange = await PublicNotices(site);
        var third = await ForEditing(site);
        var removed = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, third.BaseNumber, third.Content with { Notices = [] }, null));

        // Assert
        Assert.True(changed.Succeeded);
        Assert.Equal([(NoticeKind.Closure, "Gesperrt")], afterChange.Select(n => (n.Kind, n.Text)));
        Assert.True(removed.Succeeded);
        Assert.Empty(await PublicNotices(site));
    }

    [SqlTheory]
    [InlineData(null, null, null)]
    [InlineData("Gesperrt", "2026-11-02", "2026-11-01")]
    public async Task Ein_Hinweis_braucht_deutschen_Text_und_endet_nicht_vor_seinem_Beginn(string? german, string? from, string? until)
    {
        // Arrange
        var (_, anna, site) = await Prepare();
        var edit = await ForEditing(site);
        var notice = Notice(NoticeKind.Closure, Date(from), Date(until), ("de", german), ("fr", "Fermé"));

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { Notices = [notice] }, null));

        // Assert
        Assert.Equal([EditorialError.InvalidNotice], result.Errors);
    }

    [SqlFact]
    public async Task Eine_Skizze_ist_nach_der_Freigabe_gespeichert_und_dieselbe_Skizze_erzeugt_keine_neue_Version()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var first = await ForEditing(site);
        SketchFeature[] sketch =
        [
            Sketch(SketchKind.Sculpture, 12, [("de", " Madonna "), ("fr", "")], (47.2850, 8.6080)),
            Sketch(SketchKind.Wall, null, [], (47.2851, 8.6079), (47.2853, 8.6077)),
        ];

        // Act
        var saved = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, first.BaseNumber, first.Content with { Sketch = sketch }, null));
        var second = await ForEditing(site);
        var again = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, second.BaseNumber, second.Content with { Sketch = sketch }, null));

        // Assert
        Assert.True(saved.Succeeded);
        Assert.Equal([(SketchKind.Sculpture, 12, "Madonna"), (SketchKind.Wall, (int?)null, (string?)null)],
            second.Content.SketchList.Select(f => (f.Kind, f.Depth, f.Texts.FirstOrDefault(t => t.Culture == "de")?.Label)));
        Assert.True(again.NoChange);
    }

    [SqlFact]
    public async Task Weitere_Namen_werden_getrimmt_gespeichert_und_dieselben_Namen_erzeugen_keine_neue_Version()
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var first = await ForEditing(site);

        // Act
        var saved = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, first.BaseNumber,
            first.Content with { OtherNames = [" Grosser Parkplatz ", "", "Seeterrasse"] }, null));
        var second = await ForEditing(site);
        var again = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, second.BaseNumber,
            second.Content with { OtherNames = ["Grosser Parkplatz", "Seeterrasse "] }, null));

        // Assert
        Assert.True(saved.Succeeded);
        Assert.Equal(["Grosser Parkplatz", "Seeterrasse"], second.Content.OtherNameList);
        Assert.True(again.NoChange);
    }

    [SqlTheory]
    [InlineData(6, 10, false, false)]
    [InlineData(1, 81, false, false)]
    [InlineData(2, 10, true, false)]
    [InlineData(1, 10, false, true)]
    public async Task Weitere_Namen_sind_hoechstens_fuenf_kurz_und_ohne_Dubletten_auch_nicht_gleich_wie_der_Name(int count, int length, bool duplicate, bool sameAsName)
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);
        var names = Enumerable.Range(0, count).Select(i => $"{i}{new string('x', length - 1)}").ToList();
        if (duplicate)
        {
            names[1] = names[0].ToUpperInvariant();
        }

        if (sameAsName)
        {
            names[0] = "riff williams";
        }

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { OtherNames = names }, null));

        // Assert
        Assert.Equal([EditorialError.InvalidOtherNames], result.Errors);
    }

    [SqlTheory]
    [InlineData(SketchKind.Sculpture, 2, 0, null, 10)]
    [InlineData(SketchKind.Wall, 1, 0, null, 10)]
    [InlineData(SketchKind.PlantField, 2, 0, null, 10)]
    [InlineData(SketchKind.Wreck, 1, 3000, null, 10)]
    [InlineData(SketchKind.Wreck, 1, 0, -1, 10)]
    [InlineData(SketchKind.Highlight, 1, 0, null, 81)]
    [InlineData((SketchKind)99, 1, 0, null, 10)]
    public async Task Eine_Skizze_braucht_eine_bekannte_Art_deren_Form_Punkte_nahe_am_Platz_eine_gueltige_Tiefe_und_kurze_Beschriftungen(
        SketchKind kind, int points, int metersAway, int? depth, int labelLength)
    {
        // Arrange
        var (moderator, _, site) = await Prepare();
        var edit = await ForEditing(site);
        var north = edit.Content.Latitude + metersAway / 111_000.0;
        var feature = new SketchFeature(Guid.NewGuid(), kind,
            [.. Enumerable.Range(0, points).Select(i => new SketchPoint(north + i * 0.0001, edit.Content.Longitude))], depth, [new SketchText("de", new string('a', labelLength))]);

        // Act
        var result = await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(moderator, site, edit.BaseNumber, edit.Content with { Sketch = [feature] }, null));

        // Assert
        Assert.Equal([EditorialError.InvalidSketch], result.Errors);
    }

    [SqlFact]
    public async Task Eine_Version_im_alten_Format_ohne_Einstiege_Hinweise_Skizze_und_Fotos_laesst_sich_laden()
    {
        // Arrange
        var (_, _, site) = await Prepare();
        var path = Assert.Single(Sql.Revisions.Paths);
        var json = JsonNode.Parse(await Sql.Revisions.Load(path, TestContext.Current.CancellationToken))!.AsObject();
        json.Remove("entryPoints");
        json.Remove("notices");
        await Sql.Revisions.Save(path, JsonSerializer.SerializeToUtf8Bytes(json), TestContext.Current.CancellationToken);

        // Act
        var edit = await ForEditing(site);

        // Assert
        Assert.Equal("Riff Williams", edit.Content.Name);
        Assert.Empty(edit.Content.EntryPointList);
        Assert.Empty(edit.Content.NoticeList);
        Assert.Empty(edit.Content.SketchList);
        Assert.Empty(edit.Content.OtherNameList);
        Assert.Empty(edit.Content.MediaList);
        Assert.Null(edit.Content.HeroMediaId);
    }

    private static SketchFeature Sketch(SketchKind kind, int? depth, (string Culture, string Label)[] labels, params (double Lat, double Lng)[] points) =>
        new(Guid.NewGuid(), kind, [.. points.Select(p => new SketchPoint(p.Lat, p.Lng))], depth, [.. labels.Select(l => new SketchText(l.Culture, l.Label))]);

    private async Task<string> Moderator() =>
        (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];

    private async Task<(string Moderator, string Anna, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var moderator = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var site = await AddRiffWilliams(moderator);
        var anna = await Confirmed("anna@example.org", "Anna");
        Sql.Mail.Sent.Clear();
        return (moderator, anna, site);
    }

    private async Task<string> Confirmed(string email, string displayName)
    {
        await Command<RegisterCommand, AccountResult>(new RegisterCommand(email, "Felswand-auf-25m", displayName, "de", "/c?user={user}&token={token}"));
        var link = new Uri(Sql.Mail.LinkTo(email));
        var query = link.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        await Command<ConfirmEmailCommand, AccountResult>(new ConfirmEmailCommand(query["user"], query["token"]));
        return query["user"];
    }

    private async Task<string> Moderator(string admin)
    {
        var bert = await Confirmed("bert@example.org", "Bert");
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, bert, Roles.Moderator, true));
        return bert;
    }

    private async Task<DiveSiteForEditing> ForEditing(Guid site) =>
        (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;

    private async Task<int?> PublicMaxDepth(Guid site) => (await ForEditing(site)).Content.MaxDepth;

    private async Task<IReadOnlyList<FieldChange>> Changes(Guid site, int from, int to) =>
        (await Query<CompareRevisionsQuery, IReadOnlyList<FieldChange>?>(new CompareRevisionsQuery(site, from, to, await Moderator())))!;

    private async Task<IReadOnlyList<SiteNotice>> PublicNotices(Guid site)
    {
        var slug = (await ForEditing(site)).Content.Slug;
        return ((DiveSiteLookup.Found)await Query<GetDiveSiteQuery, DiveSiteLookup>(new GetDiveSiteQuery(slug, "de"))).Site.Notices;
    }

    private static NoticeContent Notice(NoticeKind kind, DateOnly? from, DateOnly? until, params (string Culture, string? Text)[] texts) =>
        new(Guid.Empty, kind, from, until, [.. texts.Select(t => new NoticeText(t.Culture, t.Text))]);

    private static DateOnly? Date(string? value) => value is null ? null : DateOnly.Parse(value, CultureInfo.InvariantCulture);
}
