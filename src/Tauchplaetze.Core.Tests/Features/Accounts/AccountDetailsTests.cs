using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Accounts;

public class AccountDetailsTests(SqlServer sql) : SqlTest(sql)
{
    private const string ResetTemplate = "/de/account/reset-password?user={user}&token={token}";

    [SqlFact]
    public async Task Ein_Admin_sieht_Registrierung_letzte_Anmeldung_und_Anmeldemethoden_andere_sehen_nichts()
    {
        // Arrange
        var admin = await Admin();
        var registeredAt = Sql.Time.Now;
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        Sql.Time.Now = registeredAt.AddHours(2);
        await Command<SignInCommand, SignInOutcome>(new SignInCommand("anna@example.org", "Felswand-auf-25m"));

        // Act
        var details = await Query<GetAccountDetailsQuery, AccountDetails?>(new GetAccountDetailsQuery(admin, anna));
        var forAnna = await Query<GetAccountDetailsQuery, AccountDetails?>(new GetAccountDetailsQuery(anna, admin));
        var personal = await Query<GetPersonalDataQuery, PersonalData?>(new GetPersonalDataQuery(anna));

        // Assert
        Assert.Equal((registeredAt, registeredAt.AddHours(2)), (details!.CreatedAt, details.LastSignInAt));
        Assert.Equal((true, true, 0), (details.HasPassword, details.EmailConfirmed, details.AccessFailedCount));
        Assert.Equal(["Contributor"], details.Roles);
        Assert.Null(forAnna);
        Assert.Equal(registeredAt.AddHours(2), personal!.LastSignInAt);
    }

    [SqlFact]
    public async Task Ein_Admin_bestaetigt_eine_E_Mail_von_Hand_und_schickt_den_Link_fuer_ein_neues_Passwort_Mitwirkende_nicht()
    {
        // Arrange
        var admin = await Admin();
        var bert = await RegisterConfirmed("bert@example.org", "Bert");
        await Command<RegisterCommand, AccountResult>(new RegisterCommand("anna@example.org", "Felswand-auf-25m", "Anna", "de", "/c?user={user}&token={token}"));
        var anna = (await Query<GetAccountsQuery, IReadOnlyList<AccountSummary>>(new GetAccountsQuery(admin))).Single(a => a.DisplayName == "Anna").UserId;

        // Act
        var byBert = await Command<ConfirmEmailManuallyCommand, AccountResult>(new ConfirmEmailManuallyCommand(bert, anna));
        var resetBeforeConfirm = await Command<SendPasswordResetCommand, AccountResult>(new SendPasswordResetCommand(admin, anna, ResetTemplate));
        var confirmed = await Command<ConfirmEmailManuallyCommand, AccountResult>(new ConfirmEmailManuallyCommand(admin, anna));
        var signIn = await Command<SignInCommand, SignInOutcome>(new SignInCommand("anna@example.org", "Felswand-auf-25m"));
        var reset = await Command<SendPasswordResetCommand, AccountResult>(new SendPasswordResetCommand(admin, anna, ResetTemplate));

        // Assert
        Assert.Equal([AccountError.NotAllowed], byBert.Errors);
        Assert.Equal([AccountError.NotAllowed], resetBeforeConfirm.Errors);
        Assert.True(confirmed.Succeeded);
        Assert.Equal(SignInStatus.Succeeded, signIn.Status);
        Assert.True(reset.Succeeded);
        Assert.StartsWith("https://", Sql.Mail.LinkTo("anna@example.org"));
        Assert.Contains("/de/account/reset-password?user=", Sql.Mail.LinkTo("anna@example.org"));
    }

    [SqlFact]
    public async Task Ein_Admin_loescht_kein_eigenes_Konto_und_nicht_den_letzten_aktiven_Admin_Mitwirkende_loeschen_keine_fremden()
    {
        // Arrange
        var admin = await Admin();
        var anna = await RegisterConfirmed("anna@example.org", "Anna");
        var bert = await RegisterConfirmed("bert@example.org", "Bert");

        // Act
        var self = await Command<DeleteAccountByAdminCommand, AccountResult>(new DeleteAccountByAdminCommand(admin, admin));
        var byAnna = await Command<DeleteAccountByAdminCommand, AccountResult>(new DeleteAccountByAdminCommand(anna, bert));
        await Command<SetRoleCommand, AccountResult>(new SetRoleCommand(admin, anna, Roles.Admin, true));
        await Command<BlockAccountCommand, AccountResult>(new BlockAccountCommand(admin, anna, true));
        var lastActive = await Command<DeleteAccountByAdminCommand, AccountResult>(new DeleteAccountByAdminCommand(anna, admin));
        var spam = await Command<DeleteAccountByAdminCommand, AccountResult>(new DeleteAccountByAdminCommand(admin, bert));

        // Assert
        Assert.Equal([AccountError.NotAllowed], self.Errors);
        Assert.Equal([AccountError.NotAllowed], byAnna.Errors);
        Assert.Equal([AccountError.Blocked], lastActive.Errors);
        Assert.True(spam.Succeeded);
        Assert.Null(await Query<GetAccountDetailsQuery, AccountDetails?>(new GetAccountDetailsQuery(admin, bert)));
    }

    private async Task<string> Admin() =>
        (await Command<EnsureAdminsCommand, EnsureAdminsResult>(new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]))).UserIds[0];
}
