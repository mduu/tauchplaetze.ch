using Tauchplaetze.Core.Features.WaterBodies;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>
/// Die Prognose eines Platzes ab heute, mit dem Wetter pro Stunde ab heute 00:00. Leer, solange kein Lauf den Platz berechnet hat.
/// <see cref="WaterBodyType"/> bestimmt, welche Blöcke gelten (`ux.md`, Tauchplatzseite).
/// </summary>
public record SiteForecast(
    IReadOnlyList<ForecastDay> Days,
    DateTimeOffset? ComputedAt,
    IReadOnlyList<WeatherHourForecast>? Hours = null,
    WaterBodyType WaterBodyType = WaterBodyType.Lake)
{
    public IReadOnlyList<WeatherHourForecast> HourList => Hours ?? [];

    /// <summary>Temperatur nach Tiefe gibt es nur im See. Ein Fluss ist durchmischt, die Station misst an der Oberfläche.</summary>
    public bool HasDepthProfile => WaterBodyType != WaterBodyType.River && Days is [{ SurfaceTemperature: not null }, ..];

    /// <summary>Satellitenwerte (Sicht, Algen) gibt es nur für Seen.</summary>
    public bool HasSatellite => WaterBodyType != WaterBodyType.River;
}

/// <summary>Das Wetter einer Stunde am Platz, Lokalzeit. Sonne in Minuten der Stunde, Wind aus Richtung in Grad (0 = Nord).</summary>
public record WeatherHourForecast(
    DateTime LocalTime,
    decimal? TemperatureC,
    decimal? PrecipitationMm,
    int? SunshineMinutes,
    WeatherSymbol? Symbol,
    bool? IsDay,
    decimal? WindSpeedKmh,
    decimal? WindGustsKmh,
    int? WindDirection);

/// <summary>Ein Tag der Prognose. Ohne <see cref="DiveIndex"/> ist der Tag ein Ausblick.</summary>
public record ForecastDay(
    DateOnly Date,
    byte DataLevel,
    decimal? DiveIndex,
    ProfileSource? ProfileSource,
    decimal? SurfaceTemperature,
    decimal? TemperatureAt10m,
    decimal? TemperatureAt20m,
    decimal? TemperatureAt30m,
    decimal? ThermoclineTop,
    decimal? ThermoclineBottom,
    decimal? SecchiDepth,
    DateOnly? SecchiObservedAt,
    VisibilityIndicator VisibilityIndicator,
    decimal? VisibilityGoodBelow,
    int? WindBeaufort,
    decimal? WindSpeedKmh,
    decimal? WindGustsKmh,
    decimal? PrecipitationLast72h,
    AlgaeLevel AlgaeLevel,
    IReadOnlyList<ReasonCode> Reasons,
    decimal? SecchiDepthShore = null,
    ClaritySource? ClaritySource = null,
    decimal? CurrentSpeed = null,
    int? CurrentDirection = null,
    CurrentLevel? CurrentLevel = null)
{
    public bool IsOutlook => DiveIndex is null;
}

/// <summary>Schwellen des Abflusses in m³/s: ab <see cref="LowFrom"/> gering, ab <see cref="MediumFrom"/> mittel, ab <see cref="StrongFrom"/> stark.</summary>
public record DischargeThresholds(decimal LowFrom, decimal MediumFrom, decimal StrongFrom);

/// <summary>
/// Der Abfluss an einem Flussplatz von der zugeordneten BAFU-Station. <see cref="Thresholds"/>: die Schwellen, mit denen
/// <see cref="Level"/> eingeordnet ist, vom Platz (<see cref="UsesSiteThresholds"/>) oder aus dem Median der letzten 30 Tage.
/// </summary>
public record SiteDischarge(
    string StationId,
    string StationName,
    decimal Discharge,
    DateTimeOffset MeasuredAt,
    bool IsStale,
    DischargeLevel? Level,
    DischargeThresholds? Thresholds,
    bool UsesSiteThresholds,
    decimal? Median30Days,
    DischargeTrend? Trend,
    decimal? ChangeThreeHours,
    IReadOnlyList<DischargeHour> Hours,
    IReadOnlyList<DischargeDay> Days,
    IReadOnlyList<DischargeHour> ForecastHours,
    WaterTemperature? Temperature = null,
    DischargeForecast? Forecast = null);

/// <summary>
/// Die Vorhersage des BAFU für die Station. <see cref="Official"/>: vom BAFU kontrolliert, sonst nur automatisch gerechnet.
/// <see cref="Days"/>: Tagesmittel der vorhergesagten Stunden pro Schweizer Kalendertag, ab heute.
/// </summary>
public record DischargeForecast(DateTimeOffset IssuedAt, bool Official, IReadOnlyList<DischargeForecastDay> Days);

/// <summary>Vorhergesagtes Tagesmittel mit Einordnung wie der Messwert. <see cref="Level"/> fehlt ohne Schwellen.</summary>
public record DischargeForecastDay(DateOnly Date, decimal Discharge, DischargeLevel? Level);

/// <summary>Die neueste Wassertemperatur der Station in °C. Fehlt, wenn die Station keine Temperatur misst.</summary>
public record WaterTemperature(decimal Value, DateTimeOffset MeasuredAt, bool IsStale);

/// <summary>Mittlerer Abfluss einer Stunde, Beginn in UTC.</summary>
public record DischargeHour(DateTimeOffset Hour, decimal Discharge);

/// <summary>Mittlerer Abfluss eines Schweizer Kalendertags.</summary>
public record DischargeDay(DateOnly Date, decimal Discharge);

/// <summary>
/// Der Verlauf eines Platzes bis heute, ältester Tag zuerst. Pro Tag gilt die Prognose vom Morgen dieses Tages (der letzte Lauf des Tages).
/// <see cref="RuleChanges"/>: Tage, an denen eine neue Regel gilt (`ModelVersion`). Davor und danach sind die Werte nicht direkt vergleichbar.
/// </summary>
public record SiteIndexHistory(IReadOnlyList<HistoryDay> Days, IReadOnlyList<RuleChange> RuleChanges);

/// <summary>Ein Tag im Verlauf. <see cref="SmoothedIndex"/> ist das gleitende 7-Tage-Mittel.</summary>
public record HistoryDay(
    DateOnly Date,
    decimal? DiveIndex,
    decimal? SmoothedIndex,
    decimal? SecchiDepthShore,
    decimal? SecchiDepth,
    ClaritySource? ClaritySource,
    int? WindBeaufort,
    decimal? WindGustsKmh,
    AlgaeLevel AlgaeLevel,
    string ModelVersion);

/// <summary>Ab <see cref="Date"/> gilt <see cref="ModelVersion"/>.</summary>
public record RuleChange(DateOnly Date, string ModelVersion);

/// <summary>Der Median des Index eines Gewässers, ein Eintrag pro Jahr (ältestes zuerst), für «Jahr über Jahr».</summary>
public record WaterBodyIndexHistory(IReadOnlyList<IndexYear> Years, IReadOnlyList<RuleChange> RuleChanges);

/// <summary>Die Tage eines Jahres mit Median und gleitendem 7-Tage-Mittel.</summary>
public record IndexYear(int Year, IReadOnlyList<IndexPoint> Points);

/// <summary>Ein Tag im Jahr. <see cref="Sites"/>: Plätze mit Index an diesem Tag.</summary>
public record IndexPoint(DateOnly Date, decimal MedianIndex, decimal? SmoothedIndex, int Sites);
