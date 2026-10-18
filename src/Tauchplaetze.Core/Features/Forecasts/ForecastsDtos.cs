namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Die Prognose eines Platzes ab heute, mit dem Wetter pro Stunde ab heute 00:00. Leer, solange kein Lauf den Platz berechnet hat.</summary>
public record SiteForecast(IReadOnlyList<ForecastDay> Days, DateTimeOffset? ComputedAt, IReadOnlyList<WeatherHourForecast>? Hours = null)
{
    public IReadOnlyList<WeatherHourForecast> HourList => Hours ?? [];
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
    decimal? SecchiDepthShore = null)
{
    public bool IsOutlook => DiveIndex is null;
}
