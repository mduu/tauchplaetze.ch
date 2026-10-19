using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Media;

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
    IReadOnlyList<EntryPointContent>? EntryPoints = null,
    IReadOnlyList<NoticeContent>? Notices = null,
    IReadOnlyList<SketchFeature>? Sketch = null,
    IReadOnlyList<MediaUse>? Media = null,
    Guid? HeroMediaId = null,
    IReadOnlyList<string>? OtherNames = null)
{
    public IReadOnlyList<MediaUse> MediaList => Media ?? [];

    /// <summary>Weitere Namen, unter denen der Platz bekannt ist («auch bekannt als»).</summary>
    public IReadOnlyList<string> OtherNameList => OtherNames ?? [];

    public IReadOnlyList<EntryPointContent> EntryPointList => EntryPoints ?? [];

    public IReadOnlyList<NoticeContent> NoticeList => Notices ?? [];

    public IReadOnlyList<SketchFeature> SketchList => Sketch ?? [];

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

/// <summary>Ein Hinweis zum Platz: Info, Warnung oder Sperrung, optional mit Zeitraum (`dive-site.md`, «Hinweise»).</summary>
public record NoticeContent(Guid Id, NoticeKind Kind, DateOnly? ValidFrom, DateOnly? ValidUntil, IReadOnlyList<NoticeText> Texts);

/// <summary>Text eines Hinweises in einer Sprache.</summary>
public record NoticeText(string Culture, string? Text);

/// <summary>Ein Foto in einer Version: Abschnitt und, bei «Einstieg», der Einstieg. Die Reihenfolge der Liste ist die Reihenfolge im Abschnitt.</summary>
public record MediaUse(Guid MediaId, MediaSection Section, Guid? EntryPointId = null);

/// <summary>Ein Element der Tauchskizze: Art, Punkte (WGS84), optional Tiefe in Metern und Beschriftung pro Sprache.</summary>
public record SketchFeature(Guid Id, SketchKind Kind, IReadOnlyList<SketchPoint> Points, int? Depth, IReadOnlyList<SketchText> Texts);

/// <summary>Ein Punkt der Skizze.</summary>
public record SketchPoint(double Latitude, double Longitude);

/// <summary>Beschriftung eines Skizzenelements in einer Sprache.</summary>
public record SketchText(string Culture, string? Label);

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

    /// <summary>Der Slug ist ungültig, reserviert oder vergeben.</summary>
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

    /// <summary>Ein Hinweis hat keinen deutschen Text, einen zu langen Text oder endet vor seinem Beginn.</summary>
    InvalidNotice,

    /// <summary>Zu viele weitere Namen (höchstens 5), einer ist zu lang (höchstens 80 Zeichen) oder kommt doppelt vor.</summary>
    InvalidOtherNames,

    /// <summary>Die Skizze hat zu viele Elemente, eine falsche Form, Punkte weit weg vom Platz, eine ungültige Tiefe oder zu lange Beschriftung.</summary>
    InvalidSketch,

    /// <summary>Ein Foto gehört nicht zu diesem Platz, kommt doppelt vor, der Hero fehlt in der Liste oder ein Einstieg passt nicht.</summary>
    InvalidMedia,

    /// <summary>Die Rechte am Foto sind nicht bestätigt.</summary>
    RightsNotConfirmed,

    /// <summary>Fotograf fehlt oder ist zu lang.</summary>
    InvalidPhotographer,

    /// <summary>Der Link zum Fotografen ist keine absolute http- oder https-Adresse oder länger als 300 Zeichen.</summary>
    InvalidPhotographerUrl,

    /// <summary>Die Datei ist kein JPEG, PNG oder WebP, ist grösser als 25 MB oder hat mehr als 12 000 px pro Seite oder 50 Megapixel.</summary>
    InvalidImage,

    /// <summary>Zu viele Uploads in der letzten Stunde oder am letzten Tag.</summary>
    TooManyUploads,

    /// <summary>Ein neuer Platz hat keine Koordinate.</summary>
    MissingLocation,
}

/// <summary>Ergebnis eines Commands der Redaktion. <see cref="NoChange"/>: Der Inhalt war gleich wie die Basis, es entstand keine Version.</summary>
public record EditorialResult(IReadOnlyList<EditorialError> Errors, Guid? DiveSiteId = null, int? Number = null, RevisionStatus? Status = null, bool NoChange = false)
{
    public bool Succeeded => Errors.Count == 0;

    public static EditorialResult Fail(params EditorialError[] errors) => new(errors);
}
