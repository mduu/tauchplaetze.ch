namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Der neueste Pegel einer Seestation des BAFU und der übliche Bereich für einen Kalendertag (`forecast.md`, «Seepegel»).</summary>
internal class LakeLevelReading
{
    public required string StationId { get; set; }

    public DateTimeOffset MeasuredAt { get; set; }

    /// <summary>Pegel in m ü. M.</summary>
    public decimal Level { get; set; }

    /// <summary>Der Kalendertag der Statistik in <see cref="NormLow"/>, <see cref="NormMedian"/> und <see cref="NormHigh"/>.</summary>
    public DateOnly? NormDay { get; set; }

    /// <summary>5. Perzentil der langjährigen Tageswerte für <see cref="NormDay"/>.</summary>
    public decimal? NormLow { get; set; }

    public decimal? NormMedian { get; set; }

    /// <summary>95. Perzentil der langjährigen Tageswerte für <see cref="NormDay"/>.</summary>
    public decimal? NormHigh { get; set; }

    /// <summary>Letzter Abruf der Statistik, auch ohne Ergebnis. Ein Abruf pro Tag.</summary>
    public DateOnly? NormFetchedOn { get; set; }

    public DateTimeOffset ImportedAt { get; set; }
}
