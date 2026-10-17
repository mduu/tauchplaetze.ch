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

/// <summary>Ein öffentlicher Platz, mit dem Namen des Gewässers in der gewünschten Sprache.</summary>
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
    DateTimeOffset UpdatedAt);
