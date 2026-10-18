namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Die Prognose eines Platzes ab heute. Leer, solange kein Lauf den Platz berechnet hat.</summary>
public record SiteForecast(IReadOnlyList<ForecastDay> Days, DateTimeOffset? ComputedAt);

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
    IReadOnlyList<ReasonCode> Reasons)
{
    public bool IsOutlook => DiveIndex is null;

    /// <summary>Anzug nach der Temperatur auf 20 m, sonst an der Oberfläche (`forecast.md`, «Anzug»).</summary>
    public SuitAdvice? Suit => (TemperatureAt20m ?? SurfaceTemperature) switch
    {
        null => null,
        < 14 => SuitAdvice.DrySuit,
        < 20 => SuitAdvice.WetSuit7,
        _ => SuitAdvice.WetSuit5,
    };
}
