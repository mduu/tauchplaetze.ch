namespace Tauchplaetze.Core.Features.Contact;

/// <summary>Empfänger des Kontaktformulars (`Operator:Email`). Die Adresse erscheint nie auf der Website.</summary>
public class ContactOptions
{
    public const string Section = "Operator";

    public string? Email { get; set; }
}

/// <summary>Sperrliste des Kontaktformulars (`Contact`). Sie steht nur in der Konfiguration des Betriebs, nie im Repository.</summary>
public class ContactSpamOptions
{
    public const string Section = "Contact";

    /// <summary>
    /// Domains, getrennt mit Komma oder Leerzeichen, z.B. `example.org, example.net`. Was nicht wie eine Domain aussieht, zählt nicht:
    /// Eine ungelöste Key-Vault-Referenz sperrt so nichts.
    /// </summary>
    public string? BlockedDomains { get; set; }

    /// <summary>Die Adresse liegt in einer gesperrten Domain (auch Subdomain), oder ein Text nennt eine.</summary>
    public bool Blocks(string email, params string?[] texts)
    {
        var domains = (BlockedDomains ?? "").Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(IsDomain);
        var host = email[(email.LastIndexOf('@') + 1)..];
        return domains.Any(domain =>
            host.Equals(domain, StringComparison.OrdinalIgnoreCase)
            || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase)
            || texts.Any(text => text?.Contains(domain, StringComparison.OrdinalIgnoreCase) == true));
    }

    private static bool IsDomain(string value) => value.Contains('.') && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-');
}
