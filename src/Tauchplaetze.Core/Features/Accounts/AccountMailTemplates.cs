using System.Globalization;
using Tauchplaetze.Core.CrossCutting.Mail.Templates;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Modell der Mail «E-Mail-Adresse bestätigen».</summary>
internal record ConfirmEmailMail(string DisplayName, string Link);

/// <summary>Modell der Mail «Neues Passwort».</summary>
internal record ResetPasswordMail(string DisplayName, string Link);

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
