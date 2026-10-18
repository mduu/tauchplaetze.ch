namespace Tauchplaetze.Web.Features.About;

/// <summary>
/// Betreiber für das Impressum (`infrastructure.md`). Fehlen Name oder Adresse, zeigt die Seite «Angaben folgen».
/// Die E-Mail des Betreibers (`Operator:Email`) liest nur der Core als Empfänger des Kontaktformulars (`ContactOptions`).
/// </summary>
internal class OperatorOptions
{
    public const string Section = "Operator";

    public string? Name { get; set; }

    /// <summary>Adresse, Zeilen getrennt durch Zeilenumbruch.</summary>
    public string? Address { get; set; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Address);

    public IEnumerable<string> AddressLines => (Address ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
