using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Mittlerer Abfluss einer BAFU-Station in einer Stunde (UTC).</summary>
internal class HydroHour
{
    public required string StationId { get; set; }

    public HydroSeriesKind Kind { get; set; }

    /// <summary>Beginn der Stunde in UTC.</summary>
    public DateTimeOffset Hour { get; set; }

    /// <summary>Abfluss in m³/s.</summary>
    public decimal Discharge { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
