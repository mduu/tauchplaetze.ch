namespace Tauchplaetze.Core.Features.Content;

/// <summary>Warum ein Export oder Import nicht lief.</summary>
public enum ContentError
{
    /// <summary>Nur Admins dürfen das.</summary>
    NotAllowed,

    /// <summary>Der Import (bzw. der Abruf per Schlüssel) ist in dieser Umgebung ausgeschaltet.</summary>
    Disabled,

    /// <summary>Der Schlüssel für den Abruf stimmt nicht.</summary>
    InvalidKey,

    /// <summary>Die Datei ist kein lesbares Inhaltspaket, hat eine unbekannte Formatversion oder ist zu gross.</summary>
    InvalidPackage,

    /// <summary>Quelle oder Schlüssel für den Abruf sind nicht konfiguriert.</summary>
    SourceNotConfigured,

    /// <summary>Die Quelle hat das Paket nicht geliefert.</summary>
    SourceFailed,
}

/// <summary>Ergebnis eines Exports. Die Zahlen beschreiben den Inhalt des Pakets.</summary>
public record ExportContentResult(ContentError? Error, int WaterBodies = 0, int DiveSites = 0, int Photos = 0);

/// <summary>Ergebnis eines Imports. <see cref="SitesSkipped"/>: Gewässer unbekannt oder Slug lokal von einem anderen Platz belegt.</summary>
public record ImportContentResult(
    ContentError? Error,
    int WaterBodiesAdded = 0,
    int SitesAdded = 0,
    int SitesUpdated = 0,
    int SitesUnchanged = 0,
    int SitesSkipped = 0,
    int PhotosAdded = 0)
{
    public static ImportContentResult Fail(ContentError error) => new(error);
}

/// <summary>Was in dieser Umgebung möglich ist, für die Seite `/platform/inhalt`.</summary>
public record ContentSyncStatus(bool ImportEnabled, bool ExportKeyConfigured, string? SourceHost);
