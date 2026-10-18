namespace Tauchplaetze.Core.Features.Media;

/// <summary>Abschnitt der Platzseite, zu dem ein Foto gehört (`dive-site.md`, «Bilder pro Abschnitt»). Über Wasser heisst Umgebung oder Anfahrt.</summary>
public enum MediaSection
{
    /// <summary>Unter Wasser.</summary>
    Underwater = 0,

    /// <summary>Umgebung, über Wasser.</summary>
    Surroundings = 1,

    /// <summary>Anfahrt und Parkplatz.</summary>
    Approach = 2,

    /// <summary>Ein Einstieg.</summary>
    EntryPoint = 3,

    /// <summary>Gefahren.</summary>
    Hazards = 4,
}

/// <summary>Art eines Mediums. Im MVP gibt es nur Fotos.</summary>
public enum MediaKind
{
    /// <summary>Foto.</summary>
    Photo = 0,

    /// <summary>Video (später).</summary>
    Video = 1,
}
