namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Der neueste Messwert des Abflusses einer BAFU-Station, mit Vergleichswerten aus dem Import.</summary>
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

    public DateTimeOffset ImportedAt { get; set; }
}
