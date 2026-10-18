namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Sichtbarkeit eines Tauchplatzes (`cms.md`, «Status eines Platzes»).</summary>
public enum DiveSiteStatus
{
    /// <summary>Noch keine freigegebene Version. Nur im CMS sichtbar.</summary>
    Draft,

    /// <summary>Öffentlich.</summary>
    Published,

    /// <summary>Nicht mehr öffentlich.</summary>
    Archived,
}

/// <summary>Zugang zum Tauchplatz.</summary>
public enum DiveSiteAccess
{
    /// <summary>Vom Ufer.</summary>
    Shore,

    /// <summary>Vom Boot.</summary>
    Boat,

    /// <summary>Vom Ufer und vom Boot.</summary>
    ShoreAndBoat,
}

/// <summary>Eigenschaften eines Platzes. Wand und Wrack kommen aus den Bewertungen.</summary>
[Flags]
public enum DiveSiteFeatures
{
    /// <summary>Keine.</summary>
    None = 0,

    /// <summary>Plateau.</summary>
    Plateau = 1,

    /// <summary>Höhle.</summary>
    Cave = 2,

    /// <summary>Flusstauchgang.</summary>
    River = 4,

    /// <summary>Eistauchen möglich.</summary>
    IceDiving = 8,

    /// <summary>Geeignet für Ausbildung.</summary>
    Training = 16,
}

/// <summary>Art eines Orts am Platz (`dive-site.md`, «Einstiege und Orte»): ein Weg ins Wasser oder ein Ort an Land.</summary>
public enum EntryPointKind
{
    /// <summary>Ein- und Ausstieg.</summary>
    EntryAndExit,

    /// <summary>Nur Einstieg.</summary>
    Entry,

    /// <summary>Nur Ausstieg.</summary>
    Exit,

    /// <summary>Parkplatz.</summary>
    Parking,

    /// <summary>Toilette.</summary>
    Toilet,
}

/// <summary>Regeln zur Art eines Orts.</summary>
public static class EntryPointKinds
{
    /// <summary>Ein Weg ins Wasser oder aus dem Wasser, kein Ort an Land.</summary>
    public static bool IsWaterAccess(this EntryPointKind kind) => kind is EntryPointKind.EntryAndExit or EntryPointKind.Entry or EntryPointKind.Exit;
}

/// <summary>Art eines Hinweises zum Platz (`dive-site.md`, «Hinweise»). Die Reihenfolge ist die Schwere.</summary>
public enum NoticeKind
{
    /// <summary>Information, z.B. Bauarbeiten in der Nähe.</summary>
    Info,

    /// <summary>Warnung, z.B. Parkplatz nicht zugänglich.</summary>
    Warning,

    /// <summary>Sperrung: Am Platz darf man nicht tauchen. Der Platz fehlt in der Rangliste.</summary>
    Closure,
}
