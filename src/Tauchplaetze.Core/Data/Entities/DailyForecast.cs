using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Data.Entities;

internal class DailyForecast
{
    public Guid DiveSiteId { get; set; }

    public DateOnly Date { get; set; }

    public byte DataLevel { get; set; }

    public decimal? DiveIndex { get; set; }

    public ProfileSource? ProfileSource { get; set; }

    public decimal? SurfaceTemperature { get; set; }

    public decimal? TemperatureAt10m { get; set; }

    public decimal? TemperatureAt20m { get; set; }

    public decimal? TemperatureAt30m { get; set; }

    public decimal? ThermoclineTop { get; set; }

    public decimal? ThermoclineBottom { get; set; }

    public decimal? SecchiDepth { get; set; }

    /// <summary>Secchi-Schätzung in Ufernähe am Platz (`rules-1.1`). Leer bei Zeilen von `rules-1.0`.</summary>
    public decimal? SecchiDepthShore { get; set; }

    public DateOnly? SecchiObservedAt { get; set; }

    /// <summary>Woher die Klarheit kommt (`rules-1.2`). Leer bei älteren Zeilen: dann Sentinel-3.</summary>
    public ClaritySource? ClaritySource { get; set; }

    public VisibilityIndicator VisibilityIndicator { get; set; }

    public decimal? VisibilityGoodBelow { get; set; }

    public int? WindBeaufort { get; set; }

    public decimal? WindSpeedKmh { get; set; }

    public decimal? WindGustsKmh { get; set; }

    public int? WindDirection { get; set; }

    public string? WeatherModel { get; set; }

    /// <summary>Stärkste Strömung im Tauchfenster, über 5–30 m gemittelt, in m/s. Nur bei Seen mit 3D-Modell.</summary>
    public decimal? CurrentSpeed { get; set; }

    /// <summary>Richtung, in die die Strömung fliesst, in Grad (0 = Nord).</summary>
    public int? CurrentDirection { get; set; }

    public CurrentLevel? CurrentLevel { get; set; }

    public decimal? PrecipitationLast72h { get; set; }

    public decimal? PrecipitationOnDay { get; set; }

    public decimal? InflowDischargeRatio { get; set; }

    public decimal? Turbidity { get; set; }

    public AlgaeLevel AlgaeLevel { get; set; }

    public string ReasonCodes { get; set; } = "";

    public required string ModelVersion { get; set; }

    public Guid ForecastRunId { get; set; }

    public DateTimeOffset ComputedAt { get; set; }
}
