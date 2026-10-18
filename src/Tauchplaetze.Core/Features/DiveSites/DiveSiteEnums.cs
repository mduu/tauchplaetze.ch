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

/// <summary>Art eines Einstiegs (`dive-site.md`, «Einstiege»).</summary>
public enum EntryPointKind
{
    /// <summary>Ein- und Ausstieg.</summary>
    EntryAndExit,

    /// <summary>Nur Einstieg.</summary>
    Entry,

    /// <summary>Nur Ausstieg.</summary>
    Exit,
}
