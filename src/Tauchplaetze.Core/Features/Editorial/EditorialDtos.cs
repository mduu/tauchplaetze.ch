using Tauchplaetze.Core.Features.DiveSites;

namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Der ganze Inhalt eines Platzes, wie die Redaktion ihn bearbeitet. Eine Version speichert genau diesen Inhalt (ADR-008).</summary>
public record DiveSiteContent(
    string Name,
    string Slug,
    int WaterBodyId,
    double Latitude,
    double Longitude,
    string? Municipality,
    string? Canton,
    int? MaxDepth,
    byte? DifficultyRating,
    byte? WallRating,
    byte? MarineLifeRating,
    byte? WrecksRating,
    byte? CurrentRating,
    byte? SurroundingsRating,
    DiveSiteAccess? Access,
    DiveSiteFeatures Features,
    bool HasDivingBan,
    string? InflowStationOverride,
    double? SatelliteLatitude,
    double? SatelliteLongitude,
    bool ForecastDisabled,
    IReadOnlyList<DiveSiteContentTranslation> Translations,
    IReadOnlyList<EntryPointContent>? EntryPoints = null)
{
    public IReadOnlyList<EntryPointContent> EntryPointList => EntryPoints ?? [];

    public DiveSiteContentTranslation? TextIn(string culture) => Translations.FirstOrDefault(t => t.Culture == culture);
}

/// <summary>Texte eines Platzes in einer Sprache.</summary>
public record DiveSiteContentTranslation(
    string Culture,
    string? UnderwaterDescription,
    string? SurroundingsInfo,
    string? ApproachInfo,
    string? HazardsInfo,
    string? FacilitiesInfo,
    string? DivingBanNote,
    bool IsMachineTranslated);

/// <summary>Ein Einstieg mit Pin und Texten. Der erste ist der Hauptzugang.</summary>
public record EntryPointContent(Guid Id, double Latitude, double Longitude, EntryPointKind Kind, IReadOnlyList<EntryPointText> Texts);

/// <summary>Name und Beschreibung eines Einstiegs in einer Sprache.</summary>
public record EntryPointText(string Culture, string? Name, string? Description);

/// <summary>Fehler beim Speichern oder Freigeben. Die Oberfläche bildet daraus Texte.</summary>
public enum EditorialError
{
    /// <summary>Name fehlt oder ist länger als 120 Zeichen.</summary>
    InvalidName,

    /// <summary>Das Gewässer gibt es nicht.</summary>
    UnknownWaterBody,

    /// <summary>Die Koordinate liegt nicht in der Schweiz.</summary>
    LocationOutsideSwitzerland,

    /// <summary>Eine Bewertung liegt nicht zwischen 1 und 5.</summary>
    InvalidRating,

    /// <summary>Der Slug ist ungültig oder vergeben.</summary>
    InvalidSlug,

    /// <summary>Zu viele offene Vorschläge (höchstens 20).</summary>
    TooManyPending,

    /// <summary>Die Version wartet nicht mehr auf Freigabe.</summary>
    NotPending,

    /// <summary>Nur Moderation und Admin dürfen das.</summary>
    NotAllowed,

    /// <summary>Den Platz oder die Version gibt es nicht.</summary>
    NotFound,

    /// <summary>Das Konto ist gesperrt.</summary>
    Blocked,
}

/// <summary>Ergebnis eines Commands der Redaktion. <see cref="NoChange"/>: Der Inhalt war gleich wie die Basis, es entstand keine Version.</summary>
public record EditorialResult(IReadOnlyList<EditorialError> Errors, Guid? DiveSiteId = null, int? Number = null, RevisionStatus? Status = null, bool NoChange = false)
{
    public bool Succeeded => Errors.Count == 0;

    public static EditorialResult Fail(params EditorialError[] errors) => new(errors);
}
