namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Fehler bei Registrierung, Profil oder Passwort. Die Oberfläche bildet daraus Texte.</summary>
public enum AccountError
{
    /// <summary>Die Registrierung ist geschlossen.</summary>
    RegistrationClosed,

    /// <summary>Die E-Mail-Adresse ist ungültig.</summary>
    InvalidEmail,

    /// <summary>Zu dieser E-Mail-Adresse gibt es schon ein Konto.</summary>
    EmailTaken,

    /// <summary>Der Anzeigename ist leer oder zu lang (höchstens 60 Zeichen).</summary>
    InvalidDisplayName,

    /// <summary>Der Anzeigename ist vergeben.</summary>
    DisplayNameTaken,

    /// <summary>Das Passwort ist kürzer als 10 Zeichen.</summary>
    PasswordTooShort,

    /// <summary>Der Link ist ungültig oder abgelaufen.</summary>
    InvalidToken,

    /// <summary>Dem Konto fehlt das Recht für diese Aktion.</summary>
    NotAllowed,

    /// <summary>Das Konto ist gesperrt und ändert nichts mehr.</summary>
    Blocked,

    /// <summary>Unbekannter Fehler von Identity.</summary>
    Other,
}

/// <summary>Ergebnis eines Commands der Konten. Leer heisst: erfolgreich.</summary>
public record AccountResult(IReadOnlyList<AccountError> Errors)
{
    public static readonly AccountResult Ok = new([]);

    public bool Succeeded => Errors.Count == 0;

    public static AccountResult Fail(params AccountError[] errors) => new(errors);
}

/// <summary>Ein angemeldetes Konto: Werte für das Cookie der Web-App.</summary>
public record SignedInAccount(string UserId, string DisplayName, string Culture, IReadOnlyList<string> Roles);

/// <summary>Ergebnis der Anmeldung.</summary>
public enum SignInStatus
{
    /// <summary>Angemeldet.</summary>
    Succeeded,

    /// <summary>E-Mail oder Passwort falsch.</summary>
    Failed,

    /// <summary>Die E-Mail-Adresse ist noch nicht bestätigt.</summary>
    NotConfirmed,

    /// <summary>Zu viele Fehlversuche, vorübergehend gesperrt.</summary>
    LockedOut,

    /// <summary>Konto von Moderation oder Admin gesperrt.</summary>
    Blocked,
}
