namespace Tauchplaetze.Core.Features.Content;

/// <summary>Abgleich der Inhalte von Prod nach lokal (Abschnitt `Content`, ADR-011).</summary>
public class ContentOptions
{
    public const string Section = "Content";

    /// <summary>Erlaubt den Import eines Inhaltspakets. Nur lokal setzen, in Prod nie.</summary>
    public bool ImportEnabled { get; set; }

    /// <summary>Schlüssel für den automatischen Abruf des Pakets. In Prod schaltet er den Endpoint frei, lokal braucht ihn der Abruf. Ohne Wert ist der Endpoint aus.</summary>
    public string? ExportKey { get; set; }

    /// <summary>Basis-Adresse der Quelle (Prod), von der lokal das Paket kommt, z.B. `https://tauchplaetze.ch/`.</summary>
    public Uri? SourceBaseUri { get; set; }

    /// <summary>Höchstens so gross darf ein Paket sein, das der Import liest.</summary>
    public long MaxPackageBytes { get; set; } = 2L * 1024 * 1024 * 1024;
}
