using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Features.WaterBodies.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Accounts;

public class AccountTests(SqlServer sql) : SqlTest(sql)
{
    private const string Email = "anna@example.org";

    private const string Password = "Felswand-auf-25m";

    private const string ConfirmTemplate = "/de/account/confirm?user={user}&token={token}";

    private const string ResetTemplate = "/de/account/reset?user={user}&token={token}";

    [SqlFact]
    public async Task Wer_sich_registriert_ist_Mitwirkende_und_kann_sich_erst_nach_der_Bestaetigung_anmelden()
    {
        // Arrange
        await Register("Anna");

        // Act
        var beforeConfirm = await SignIn(Password);
        var confirmed = await Command<ConfirmEmailCommand, AccountResult>(Confirmation(Sql.Mail.LinkTo(Email)));
        var afterConfirm = await SignIn(Password);

        // Assert
        Assert.Equal(SignInStatus.NotConfirmed, beforeConfirm.Status);
        Assert.True(confirmed.Succeeded);
        Assert.Equal(SignInStatus.Succeeded, afterConfirm.Status);
        Assert.Equal(["Contributor"], afterConfirm.Account!.Roles);
        Assert.Equal("Anna", afterConfirm.Account.DisplayName);
    }

    [SqlFact]
    public async Task Ein_Anzeigename_ist_eindeutig()
    {
        // Arrange
        await Register("Anna");

        // Act
        var second = await Command<RegisterCommand, AccountResult>(new RegisterCommand("bert@example.org", Password, " Anna ", "de", ConfirmTemplate));

        // Assert
        Assert.Equal([AccountError.DisplayNameTaken], second.Errors);
    }

    [SqlFact]
    public async Task Ist_die_Registrierung_geschlossen_entsteht_kein_Konto()
    {
        // Arrange
        await using var scope = Sql.Services.CreateAsyncScope();
        var handler = new RegisterHandler(
            scope.ServiceProvider.GetRequiredService<UserManager<User>>(),
            scope.ServiceProvider.GetRequiredService<MailDispatcher>(),
            Options.Create(new AccountsOptions { RegistrationOpen = false }),
            Options.Create(new PublicSiteOptions()),
            Sql.Time,
            new AccountMailCooldown(Sql.Time));

        // Act
        var result = await handler.Handle(new RegisterCommand(Email, Password, "Anna", "de", ConfirmTemplate), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal([AccountError.RegistrationClosed], result.Errors);
        Assert.Empty(Sql.Mail.Sent);
    }

    [SqlFact]
    public async Task Eine_Registrierung_mit_bekannter_Adresse_meldet_Erfolg_und_schickt_nur_den_Hinweis_auf_das_bestehende_Konto()
    {
        // Arrange
        await RegisterAndConfirm();
        Sql.Time.Now += AccountMailCooldown.Cooldown;
        var before = Sql.Mail.Sent.Count;

        // Act
        var result = await Command<RegisterCommand, AccountResult>(new RegisterCommand(Email, "Anderes-Passwort-1", "Anna Zwei", "de", ConfirmTemplate));

        // Assert
        Assert.True(result.Succeeded);
        var mail = Assert.Single(Sql.Mail.Sent.Skip(before));
        Assert.Equal(Email, mail.To);
        Assert.Contains("schon", mail.Subject, StringComparison.Ordinal);
        Assert.Equal(SignInStatus.Succeeded, (await SignIn(Password)).Status);
    }

    [SqlFact]
    public async Task Ein_zweiter_Link_fuer_ein_neues_Passwort_geht_erst_nach_15_Minuten_hinaus()
    {
        // Arrange
        await RegisterAndConfirm();
        var before = Sql.Mail.Sent.Count;

        // Act
        await Command<RequestPasswordResetCommand, Unit>(new RequestPasswordResetCommand(Email, ResetTemplate));
        await Command<RequestPasswordResetCommand, Unit>(new RequestPasswordResetCommand(Email, ResetTemplate));
        var afterTwo = Sql.Mail.Sent.Count - before;
        Sql.Time.Now += AccountMailCooldown.Cooldown;
        await Command<RequestPasswordResetCommand, Unit>(new RequestPasswordResetCommand(Email, ResetTemplate));

        // Assert
        Assert.Equal(1, afterTwo);
        Assert.Equal(2, Sql.Mail.Sent.Count - before);
    }

    [SqlFact]
    public async Task Ein_Passwort_unter_10_Zeichen_wird_abgelehnt()
    {
        // Act
        var result = await Command<RegisterCommand, AccountResult>(new RegisterCommand(Email, "kurz", "Anna", "de", ConfirmTemplate));

        // Assert
        Assert.Contains(AccountError.PasswordTooShort, result.Errors);
    }

    [SqlFact]
    public async Task Nach_5_falschen_Passwoertern_ist_das_Konto_voruebergehend_gesperrt()
    {
        // Arrange
        await RegisterAndConfirm();
        for (var attempt = 0; attempt < 4; attempt++)
        {
            await SignIn("falsches-passwort");
        }

        // Act
        var fifth = await SignIn("falsches-passwort");
        var correct = await SignIn(Password);

        // Assert
        Assert.Equal(SignInStatus.LockedOut, fifth.Status);
        Assert.Equal(SignInStatus.LockedOut, correct.Status);
    }

    [SqlFact]
    public async Task Mit_dem_Link_aus_der_Mail_setzt_man_ein_neues_Passwort()
    {
        // Arrange
        await RegisterAndConfirm();
        await Command<RequestPasswordResetCommand, Unit>(new RequestPasswordResetCommand(Email, ResetTemplate));
        var (userId, token) = Parts(Sql.Mail.LinkTo(Email));

        // Act
        var reset = await Command<ResetPasswordCommand, AccountResult>(new ResetPasswordCommand(userId, token, "Neues-Passwort-2026"));

        // Assert
        Assert.True(reset.Succeeded);
        Assert.Equal(SignInStatus.Succeeded, (await SignIn("Neues-Passwort-2026")).Status);
        Assert.Equal(SignInStatus.Failed, (await SignIn(Password)).Status);
    }

    [SqlFact]
    public async Task Ein_manipulierter_Link_bestaetigt_nichts()
    {
        // Arrange
        await Register("Anna");
        var (userId, _) = Parts(Sql.Mail.LinkTo(Email));

        // Act
        var result = await Command<ConfirmEmailCommand, AccountResult>(new ConfirmEmailCommand(userId, "dGVzdA"));

        // Assert
        Assert.Equal([AccountError.InvalidToken], result.Errors);
    }

    [SqlFact]
    public async Task Wird_ein_Konto_geloescht_bleiben_die_Versionen_ohne_Autor_und_die_Auskunft_zeigt_vorher_die_Beitraege()
    {
        // Arrange
        await Command<SeedWaterBodiesCommand, SeedWaterBodiesResult>(new SeedWaterBodiesCommand());
        var admin = (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin"), new AdminAccount("zweit@example.org", "Zweit")]))).UserIds[0];
        await AddRiffWilliams(admin);
        var before = await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(admin));

        // Act
        var deleted = await Command<DeleteAccountCommand, AccountResult>(new DeleteAccountCommand(admin));

        // Assert
        Assert.Contains(before!.Contributions, c => c.SiteSlug == "riff-williams-herrliberg" && c.Number == 1);
        Assert.True(deleted.Succeeded);
        Assert.Null(await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(admin)));
        await using var scope = Sql.Services.CreateAsyncScope();
        var revisions = scope.ServiceProvider.GetRequiredService<Tauchplaetze.Core.Data.TauchplaetzeDbContext>().DiveSiteRevisions.ToList();
        Assert.All(revisions, r => Assert.Equal((null, null), (r.AuthorId, r.ReviewerId)));
    }

    private Task<AccountResult> Register(string displayName) =>
        Command<RegisterCommand, AccountResult>(new RegisterCommand(Email, Password, displayName, "de", ConfirmTemplate));

    private async Task RegisterAndConfirm()
    {
        await Register("Anna");
        await Command<ConfirmEmailCommand, AccountResult>(Confirmation(Sql.Mail.LinkTo(Email)));
    }

    private Task<SignInOutcome> SignIn(string password) => Command<SignInCommand, SignInOutcome>(new SignInCommand(Email, password));

    private static ConfirmEmailCommand Confirmation(string link)
    {
        var (userId, token) = Parts(link);
        return new ConfirmEmailCommand(userId, token);
    }

    private static (string UserId, string Token) Parts(string link)
    {
        var query = new Uri(link).Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        return (query["user"], query["token"]);
    }
}
