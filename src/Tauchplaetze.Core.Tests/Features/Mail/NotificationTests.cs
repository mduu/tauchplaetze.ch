using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.Editorial;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Editorial.Queries;
using Tauchplaetze.Core.Features.Mail;
using Tauchplaetze.Core.Features.Mail.Commands;
using Tauchplaetze.Core.Features.Mail.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Mail;

public class NotificationTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_Vorschlag_von_Mitwirkenden_geht_an_Moderation_und_Admins_nicht_an_die_Autorin_und_nicht_an_Gesperrte()
    {
        // Arrange
        var (admin, bert, anna, site) = await Prepare();
        var carla = await Moderator(admin, "carla@example.org", "Carla");
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, carla, true));
        var edit = await ForEditing(site);
        Sql.Mail.Sent.Clear();

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 32 }, "Tiefe ergänzt"));
        await Sql.DeliverMail();

        // Assert
        Assert.Equal(["admin@example.org", "bert@example.org"], Recipients("Neuer Vorschlag zu «Riff Williams»"));
        Assert.Contains(Sql.Mail.Sent, m => m.To == "bert@example.org" && m.Body.Contains("Tiefe ergänzt", StringComparison.Ordinal)
            && m.Body.Contains("https://tauchplaetze.test/admin/vorschlaege", StringComparison.Ordinal));
        Assert.DoesNotContain(Sql.Mail.Sent, m => m.To is "anna@example.org" or "carla@example.org");
    }

    [SqlFact]
    public async Task Eine_Aenderung_der_Moderation_geht_an_die_Admins_aber_nicht_an_die_Person_selbst()
    {
        // Arrange
        var (_, bert, _, site) = await Prepare();
        var edit = await ForEditing(site);

        // Act
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(bert, site, edit.BaseNumber, edit.Content with { MaxDepth = 40 }, null));
        await Sql.DeliverMail();

        // Assert
        Assert.Equal(["admin@example.org"], Recipients("«Riff Williams» wurde von der Moderation geändert"));
        Assert.Contains(Sql.Mail.Sent, m => m.Body.Contains("/admin/plaetze/" + site + "/versionen", StringComparison.Ordinal));
        Assert.DoesNotContain(Sql.Mail.Sent, m => m.To == "bert@example.org");
    }

    [SqlFact]
    public async Task Ist_die_Benachrichtigung_fuer_Admins_ausgeschaltet_bekommen_sie_keine_Mail_zu_Aenderungen_der_Moderation()
    {
        // Arrange
        var (_, bert, _, site) = await Prepare();
        await using var scope = Sql.Services.CreateAsyncScope();
        var notifications = new EditorialNotifications(
            scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            scope.ServiceProvider.GetRequiredService<MailDispatcher>(),
            scope.ServiceProvider.GetRequiredService<IOptions<PublicSiteOptions>>(),
            Options.Create(new NotificationOptions { NotifyAdminsOnModeratorChanges = false }));

        // Act
        await notifications.ModeratorChanged(new DiveSiteRevision { AuthorId = bert, Number = 2, SnapshotPath = "", SnapshotHash = "" }, site, "Riff Williams", TestContext.Current.CancellationToken);
        await Sql.DeliverMail();

        // Assert
        Assert.Empty(Sql.Mail.Sent);
    }

    [SqlFact]
    public async Task Admins_schicken_Dead_Letter_Mails_einzeln_zurueck_in_die_Queue_oder_loeschen_sie_Mitwirkende_nicht()
    {
        // Arrange
        var (admin, _, anna, _) = await Prepare();
        Sql.Mail.Sent.Clear();
        foreach (var name in new[] { "Eins", "Zwei" })
        {
            await Sql.MailQueue.DeadLetter(DeadLetterBody(name), TestContext.Current.CancellationToken);
        }

        var status = await Query<GetMailQueueStatusQuery, MailQueueStatus?>(new GetMailQueueStatusQuery(admin));
        var first = status!.Items[0].MessageId;

        // Act
        var byContributor = await Command<ReplayDeadLettersCommand, DeadLetterActionResult>(new ReplayDeadLettersCommand(anna, first));
        var replayed = await Command<ReplayDeadLettersCommand, DeadLetterActionResult>(new ReplayDeadLettersCommand(admin, first));
        await Sql.DeliverMail();
        var deleted = await Command<DeleteDeadLettersCommand, DeadLetterActionResult>(new DeleteDeadLettersCommand(admin));

        // Assert
        Assert.Equal((2, 2), (status.DeadLetters, status.Items.Count));
        Assert.True(byContributor.NotAllowed);
        Assert.Null(await Query<GetMailQueueStatusQuery, MailQueueStatus?>(new GetMailQueueStatusQuery(anna)));
        Assert.Equal(1, replayed.Count);
        Assert.Contains(Sql.Mail.Sent, m => m.Subject == "Dein Beitrag zu «Eins» ist online");
        Assert.Equal(1, deleted.Count);
        Assert.Empty(Sql.MailQueue.DeadLetters);
    }

    [SqlFact]
    public async Task Eine_erneut_gesendete_Mail_beginnt_wieder_beim_ersten_Versuch()
    {
        // Arrange
        var (admin, _, _, _) = await Prepare();
        await Sql.MailQueue.DeadLetter(DeadLetterBody("Eins"), TestContext.Current.CancellationToken);

        // Act
        await Command<ReplayDeadLettersCommand, DeadLetterActionResult>(new ReplayDeadLettersCommand(admin));
        var attemptsAfterReplay = Sql.MailQueue.Attempts;
        await Sql.DeliverMail();

        // Assert
        Assert.Equal([0L], attemptsAfterReplay);
        Assert.Contains(Sql.Mail.Sent, m => m.Subject == "Dein Beitrag zu «Eins» ist online");
        Assert.Empty(Sql.MailQueue.DeadLetters);
    }

    [SqlFact]
    public async Task Freigabe_und_Ablehnung_gehen_nicht_an_unbestaetigte_oder_gesperrte_Autorinnen()
    {
        // Arrange
        var (admin, bert, anna, site) = await Prepare();
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("dora@example.org", "Felswand-auf-25m", "Dora", "de", "/c?user={user}&token={token}"));
        var dora = (await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(admin))).Single(a => a.DisplayName == "Dora").UserId;
        var edit = await ForEditing(site);
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(dora, site, edit.BaseNumber, edit.Content with { MaxDepth = 31 }, null));
        await Command<ProposeChangeCommand, EditorialResult>(new ProposeChangeCommand(anna, site, edit.BaseNumber, edit.Content with { MaxDepth = 33 }, null));
        var pending = await Query<GetPendingRevisionsQuery, IReadOnlyList<PendingRevision>>(new GetPendingRevisionsQuery(bert));
        await Sql.DeliverMail();
        Sql.Mail.Sent.Clear();

        // Act
        await Command<ApproveRevisionCommand, EditorialResult>(new ApproveRevisionCommand(bert, pending.Single(p => p.Revision.Number == 2).Revision.Id));
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));
        await Sql.DeliverMail();

        // Assert
        Assert.DoesNotContain(Sql.Mail.Sent, m => m.To is "dora@example.org" or "anna@example.org");
    }

    private static string DeadLetterBody(string siteName)
    {
        var mail = new TestMail();
        mail.Dispatcher.Enqueue("anna@example.org", "de", new ProposalApprovedMail("Anna", siteName, "https://tauchplaetze.test/de/site/x"), CancellationToken.None).Wait();
        mail.Sender.Failing = new IOException("SMTP nicht erreichbar");
        for (var attempt = 0; attempt < MailQueueRules.MaxAttempts; attempt++)
        {
            mail.Process().Wait();
            mail.Time.Now += TimeSpan.FromHours(2);
        }

        return mail.Queue.DeadLetters.Single();
    }

    private string[] Recipients(string subjectStart) =>
        [.. Sql.Mail.Sent.Where(m => m.Subject.StartsWith(subjectStart, StringComparison.Ordinal)).Select(m => m.To).Order()];

    private async Task<(string Admin, string Bert, string Anna, Guid Site)> Prepare()
    {
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
        var site = await AddRiffWilliams(admin);
        var bert = await Moderator(admin, "bert@example.org", "Bert");
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        await Sql.DeliverMail();
        Sql.Mail.Sent.Clear();
        return (admin, bert, anna, site);
    }

    private async Task<string> Moderator(string admin, string email, string name)
    {
        var id = await RegisterConfirmed(email, name);
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, id, Roles.Moderator, true));
        return id;
    }

    private async Task<DiveSiteForEditing> ForEditing(Guid site) =>
        (await Query<GetDiveSiteForEditingQuery, DiveSiteForEditing?>(new GetDiveSiteForEditingQuery(site)))!;
}
