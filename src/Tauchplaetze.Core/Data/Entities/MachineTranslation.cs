namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Eine bezahlte maschinelle Übersetzung der Texte eines Platzes (`cms.md`, «Automatisch übersetzen»). Dient als Cache, Zähler für Limite und Budget und als Beleg für <c>IsMachineTranslated</c>.</summary>
internal class MachineTranslation
{
    public Guid Id { get; set; }

    /// <summary><c>null</c> nach der Löschung des Kontos.</summary>
    public string? RequestedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public required string SourceCulture { get; set; }

    public required string TargetCulture { get; set; }

    /// <summary>Hash von Quelltext, Sprachen, Modell und Prompt-Version. Gleiche Anfrage, gleicher Hash.</summary>
    public required string SourceHash { get; set; }

    /// <summary>Hash der übersetzten Texte mit Zielsprache. Ein Vorschlag mit genau diesen Texten bleibt «automatisch übersetzt».</summary>
    public required string ResultHash { get; set; }

    /// <summary>Die übersetzten Felder als JSON.</summary>
    public required string ResultJson { get; set; }

    public required string Model { get; set; }

    public int InputTokens { get; set; }

    public int OutputTokens { get; set; }
}
