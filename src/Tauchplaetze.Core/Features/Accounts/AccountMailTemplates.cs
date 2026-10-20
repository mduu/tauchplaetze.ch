using System.Globalization;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Modell der Mail «E-Mail-Adresse bestätigen».</summary>
internal record ConfirmEmailMail(string DisplayName, string Link);

/// <summary>Modell der Mail «Neues Passwort».</summary>
internal record ResetPasswordMail(string DisplayName, string Link);

/// <summary>Modell der Mail «Du hast schon ein Konto» bei einer Registrierung mit bekannter Adresse.</summary>
internal record AccountExistsMail(string DisplayName, string Link);

internal class ConfirmEmailTemplate : MailTemplate<ConfirmEmailMail>
{
    public override string Name => "ConfirmEmail";

    protected override MailContent Render(ConfirmEmailMail model, CultureInfo culture) => new(
        T(culture, "Confirm_Subject"),
        T(culture, "Greeting", model.DisplayName),
        [T(culture, "Confirm_Text")],
        new MailButton(T(culture, "Confirm_Button"), model.Link),
        AfterButton: [T(culture, "Confirm_Ignore")]);
}

internal class ResetPasswordTemplate : MailTemplate<ResetPasswordMail>
{
    public override string Name => "ResetPassword";

    protected override MailContent Render(ResetPasswordMail model, CultureInfo culture) => new(
        T(culture, "Reset_Subject"),
        T(culture, "Greeting", model.DisplayName),
        [T(culture, "Reset_Text")],
        new MailButton(T(culture, "Reset_Button"), model.Link),
        AfterButton: [T(culture, "Reset_Ignore")]);
}

internal class AccountExistsTemplate : MailTemplate<AccountExistsMail>
{
    public override string Name => "AccountExists";

    protected override MailContent Render(AccountExistsMail model, CultureInfo culture) => new(
        T(culture, "Exists_Subject"),
        T(culture, "Greeting", model.DisplayName),
        [T(culture, "Exists_Text")],
        new MailButton(T(culture, "Exists_Button"), model.Link),
        AfterButton: [T(culture, "Exists_Ignore")]);
}
