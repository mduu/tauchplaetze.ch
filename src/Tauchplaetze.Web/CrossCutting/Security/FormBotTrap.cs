using System.Globalization;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace Tauchplaetze.Web.CrossCutting.Security;

/// <summary>Ergebnis von <see cref="FormBotTrap.Check"/>.</summary>
internal enum FormTrapResult
{
    Human,

    /// <summary>Honeypot gefüllt oder in unter 3 s gesendet. Das Formular tut so, als hätte es geklappt, und tut nichts.</summary>
    Bot,

    /// <summary>Stempel fehlt, ist gefälscht oder älter als 6 h.</summary>
    Expired,
}

/// <summary>
/// Honeypot und Zeitfalle für öffentliche Formulare (Kontakt, Registrierung). Das Formular trägt einen signierten Zeitstempel
/// und ein Feld, das Menschen nicht sehen. Ein Bot füllt das Feld oder sendet zu schnell.
/// </summary>
internal sealed class FormBotTrap(IDataProtectionProvider protection, TimeProvider time)
{
    private static readonly TimeSpan MinFillTime = TimeSpan.FromSeconds(3);

    private static readonly TimeSpan MaxFillTime = TimeSpan.FromHours(6);

    public string NewStamp(string form) => Protector(form).Protect(time.GetUtcNow().UtcTicks.ToString(CultureInfo.InvariantCulture));

    /// <summary>Der gesendete Stempel, solange er gilt. Ein neuer würde den zweiten Versuch nach einem Fehler als «zu schnell» verwerfen.</summary>
    public string KeepStamp(string form, string? posted) =>
        posted is not null && AgeOf(form, posted) is { } age && age <= MaxFillTime ? posted : NewStamp(form);

    public FormTrapResult Check(string form, string? stamp, string? honeypot)
    {
        var age = AgeOf(form, stamp);
        if (!string.IsNullOrEmpty(honeypot) || age < MinFillTime)
        {
            return FormTrapResult.Bot;
        }

        return age is null || age > MaxFillTime ? FormTrapResult.Expired : FormTrapResult.Human;
    }

    private IDataProtector Protector(string form) => protection.CreateProtector($"Tauchplaetze.{form}.Stamp");

    private TimeSpan? AgeOf(string form, string? stamp)
    {
        try
        {
            var ticks = long.Parse(Protector(form).Unprotect(stamp ?? ""), CultureInfo.InvariantCulture);
            return time.GetUtcNow() - new DateTimeOffset(ticks, TimeSpan.Zero);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            return null;
        }
    }
}
