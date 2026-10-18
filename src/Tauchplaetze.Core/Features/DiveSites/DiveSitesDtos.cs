using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>Ergebnis der Suche nach einem Platz über seinen Slug.</summary>
public abstract record DiveSiteLookup
{
    /// <summary>Der Platz ist öffentlich.</summary>
    public sealed record Found(DiveSiteDetails Site) : DiveSiteLookup;

    /// <summary>Der Slug hat sich geändert. Die Seite leitet mit 301 auf <paramref name="Slug"/> weiter.</summary>
    public sealed record Moved(string Slug) : DiveSiteLookup;

    /// <summary>Der Platz ist archiviert (410).</summary>
    public sealed record Gone : DiveSiteLookup;

    /// <summary>Kein öffentlicher Platz mit diesem Slug (404).</summary>
    public sealed record NotFound : DiveSiteLookup;
}

/// <summary>Ein öffentlicher Platz, mit dem Namen des Gewässers in der gewünschten Sprache, den heute gültigen Hinweisen (schwerster zuerst) und den Einstiegen.</summary>
public record DiveSiteDetails(
    Guid Id,
    string Slug,
    string Name,
    string WaterBodySlug,
    string WaterBodyName,
    WaterBodyType WaterBodyType,
    string? Municipality,
    string? Canton,
    double Latitude,
    double Longitude,
    byte ContentLevel,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<SiteNotice> Notices,
    IReadOnlyList<SiteEntryPoint> EntryPoints,
    SiteRatings? Ratings = null,
    IReadOnlyList<SiteSketchFeature>? Sketch = null)
{
    public IReadOnlyList<SiteSketchFeature> SketchList => Sketch ?? [];
}

/// <summary>Ein Element der Tauchskizze mit der Beschriftung in der gewünschten Sprache, sonst auf Deutsch, sonst ohne.</summary>
public record SiteSketchFeature(SketchKind Kind, IReadOnlyList<SitePoint> Points, int? Depth, string? Label);

/// <summary>Ein Punkt in WGS84.</summary>
public record SitePoint(double Latitude, double Longitude);

/// <summary>Die Bewertungen eines Platzes, je 1 bis 5. Leer heisst «nicht bewertet» (`dive-site.md`, «Bewertungen»).</summary>
public record SiteRatings(byte? Difficulty, byte? Wall, byte? MarineLife, byte? Wrecks, byte? Current, byte? Surroundings);

/// <summary>Ein gültiger Hinweis in der gewünschten Sprache. Fehlt die Übersetzung, kommt er auf Deutsch (<see cref="GermanOnly"/>).</summary>
public record SiteNotice(NoticeKind Kind, string Text, DateOnly? ValidUntil, bool GermanOnly);

/// <summary>Ein Einstieg mit dem Namen in der gewünschten Sprache, sonst auf Deutsch, sonst ohne Namen.</summary>
public record SiteEntryPoint(EntryPointKind Kind, double Latitude, double Longitude, string? Name);
