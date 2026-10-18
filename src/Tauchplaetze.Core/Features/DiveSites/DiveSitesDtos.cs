using Tauchplaetze.Core.Features.Media;
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
    IReadOnlyList<SiteSketchFeature>? Sketch = null,
    IReadOnlyList<PublicPhoto>? Photos = null,
    Guid? HeroMediaId = null)
{
    public IReadOnlyList<SiteSketchFeature> SketchList => Sketch ?? [];

    /// <summary>Die öffentlichen Fotos der freigegebenen Version, nach Abschnitt und Reihenfolge.</summary>
    public IReadOnlyList<PublicPhoto> PhotoList => Photos ?? [];

    /// <summary>Das Hero-Foto, falls gesetzt und öffentlich.</summary>
    public PublicPhoto? Hero => PhotoList.FirstOrDefault(p => p.Id == HeroMediaId);
}

/// <summary>Ein Element der Tauchskizze mit der Beschriftung in der gewünschten Sprache, sonst auf Deutsch, sonst ohne.</summary>
public record SiteSketchFeature(SketchKind Kind, IReadOnlyList<SitePoint> Points, int? Depth, string? Label);

/// <summary>Ein Punkt in WGS84.</summary>
public record SitePoint(double Latitude, double Longitude);

/// <summary>Die Bewertungen eines Platzes, je 1 bis 5. Leer heisst «nicht bewertet» (`dive-site.md`, «Bewertungen»).</summary>
public record SiteRatings(byte? Difficulty, byte? Wall, byte? MarineLife, byte? Wrecks, byte? Current, byte? Surroundings)
{
    /// <summary>Nur bewertete Skalen in fester Reihenfolge. Schwierigkeit und Strömung sind Stufen, die übrigen Qualitäten.</summary>
    public IReadOnlyList<RatedScale> Rated =>
        new (RatingScale Scale, byte? Value)[]
            {
                (RatingScale.Difficulty, Difficulty),
                (RatingScale.Wall, Wall),
                (RatingScale.MarineLife, MarineLife),
                (RatingScale.Wrecks, Wrecks),
                (RatingScale.Current, Current),
                (RatingScale.Surroundings, Surroundings),
            }
            .Where(s => s.Value is >= 1 and <= 5)
            .Select(s => new RatedScale(s.Scale, s.Value!.Value, s.Scale is RatingScale.Difficulty or RatingScale.Current))
            .ToList();
}

/// <summary>Eine Bewertungsskala (`dive-site.md`, «Bewertungen»).</summary>
public enum RatingScale
{
    /// <summary>Schwierigkeit.</summary>
    Difficulty,

    /// <summary>Steilwand.</summary>
    Wall,

    /// <summary>UW-Leben.</summary>
    MarineLife,

    /// <summary>Wracks und Gegenstände.</summary>
    Wrecks,

    /// <summary>Strömung.</summary>
    Current,

    /// <summary>Umgebung.</summary>
    Surroundings,
}

/// <summary>Eine bewertete Skala. <see cref="IsLevel"/>: eine Stufe (mehr ist anspruchsvoller), sonst eine Qualität (mehr ist besser).</summary>
public record RatedScale(RatingScale Scale, byte Value, bool IsLevel);

/// <summary>Ein gültiger Hinweis in der gewünschten Sprache. Fehlt die Übersetzung, kommt er auf Deutsch (<see cref="GermanOnly"/>).</summary>
public record SiteNotice(NoticeKind Kind, string Text, DateOnly? ValidUntil, bool GermanOnly);

/// <summary>Ein Einstieg mit dem Namen in der gewünschten Sprache, sonst auf Deutsch, sonst ohne Namen.</summary>
public record SiteEntryPoint(EntryPointKind Kind, double Latitude, double Longitude, string? Name);
