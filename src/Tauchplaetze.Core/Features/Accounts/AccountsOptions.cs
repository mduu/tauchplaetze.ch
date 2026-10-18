namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Einstellungen der Konten (`Accounts`, `cms.md`, «Rollen»).</summary>
public class AccountsOptions
{
    public const string Section = "Accounts";

    /// <summary>Selbstregistrierung für Mitwirkende. Im MVP aus, ab Phase 1 an. Lokal mit Aspire an.</summary>
    public bool RegistrationOpen { get; set; }
}
