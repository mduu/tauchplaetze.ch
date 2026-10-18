namespace Tauchplaetze.Web.Features.About;

/// <summary>
/// Betreiber für das Impressum (`infrastructure.md`). Fehlen Name oder Adresse, zeigt die Seite «Angaben folgen».
/// <see cref="Email"/> ist nur der Empfänger des Kontaktformulars und erscheint nie auf der Website.
/// </summary>
internal class OperatorOptions
{
    public const string Section = "Operator";

    public string? Name { get; set; }

    /// <summary>Adresse, Zeilen getrennt durch Zeilenumbruch.</summary>
    public string? Address { get; set; }

    public string? Email { get; set; }

    public bool IsComplete => !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Address);

    public IEnumerable<string> AddressLines => (Address ?? "").Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
