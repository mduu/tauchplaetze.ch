using System.Globalization;
using System.Resources;
using Tauchplaetze.Core.CrossCutting.Mail;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Texte der Mails in der Sprache der Person (`AccountMails.resx`, vier Sprachen).</summary>
internal static class AccountMails
{
    private static readonly ResourceManager Resources = new("Tauchplaetze.Core.Features.Accounts.AccountMails", typeof(AccountMails).Assembly);

    public static MailMessage Create(string to, string culture, string key, params object[] values)
    {
        var language = CultureInfo.GetCultureInfo(culture);
        return new MailMessage(
            to,
            Resources.GetString($"{key}_Subject", language)!,
            string.Format(language, Resources.GetString($"{key}_Body", language)!, values));
    }
}
