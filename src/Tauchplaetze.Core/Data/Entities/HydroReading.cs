namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Die neuesten Messwerte einer BAFU-Station: Abfluss mit Vergleichswerten aus dem Import und, wo gemessen, Wassertemperatur.</summary>
internal class HydroReading
{
    public required string StationId { get; set; }

    public DateTimeOffset MeasuredAt { get; set; }

    /// <summary>Abfluss in m³/s.</summary>
    public decimal Discharge { get; set; }

    /// <summary>Der Messwert 3 Stunden vor <see cref="MeasuredAt"/>, für den Trend.</summary>
    public decimal? DischargeThreeHoursBefore { get; set; }

    /// <summary>Median der Stundenwerte der letzten 30 Tage.</summary>
    public decimal? Median30Days { get; set; }

    /// <summary>Neueste Wassertemperatur in °C. <c>null</c>: Die Station misst keine Temperatur.</summary>
    public decimal? Temperature { get; set; }

    public DateTimeOffset? TemperatureMeasuredAt { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
