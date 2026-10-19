namespace Tauchplaetze.Core.Data.Entities;

/// <summary>Der Median des Index aller Plätze eines Gewässers an einem Tag. Bleibt unbegrenzt (`datastructure.md`, «Aufbewahrung»).</summary>
internal class WaterBodyDailyIndex
{
    public int WaterBodyId { get; set; }

    public DateOnly Date { get; set; }

    public decimal MedianIndex { get; set; }

    public int Sites { get; set; }

    public required string ModelVersion { get; set; }
}
