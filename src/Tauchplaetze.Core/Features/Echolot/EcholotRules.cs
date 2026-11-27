namespace Tauchplaetze.Core.Features.Echolot;

/// <summary>Ab wann eine Datenquelle verspätet oder gestört ist (`ux.md`, «Echolot»).</summary>
internal static class EcholotRules
{
    /// <summary>Der Prognose-Lauf läuft um 04:30 und 13:00. Dazwischen liegen bis zu 15.5 Stunden.</summary>
    public static readonly TimeSpan ForecastLateAfter = TimeSpan.FromHours(18);

    /// <summary>Danach gilt die Prognose als veraltet (`ux.md`, «Zustände»).</summary>
    public static readonly TimeSpan ForecastDownAfter = TimeSpan.FromHours(30);

    /// <summary>Der Abfluss-Job läuft alle 30 Minuten.</summary>
    public static readonly TimeSpan DischargeLateAfter = TimeSpan.FromHours(2);

    /// <summary>Der Pegel-Job läuft jede Stunde.</summary>
    public static readonly TimeSpan LakeLevelLateAfter = TimeSpan.FromHours(3);

    public static readonly TimeSpan MeasurementDownAfter = TimeSpan.FromHours(24);

    /// <summary>Der Abgleich mit dem SUSV läuft täglich um 04:30 (`stories/002-susv-import-und-moderation.md`).</summary>
    public static readonly TimeSpan SusvLateAfter = TimeSpan.FromHours(36);

    public static readonly TimeSpan SusvDownAfter = TimeSpan.FromHours(72);

    /// <summary>Älter ist das neueste Satellitenbild meist wegen Wolken.</summary>
    public const int SatelliteLateAfterDays = 5;

    /// <summary>So viele Einträge zeigen die Neuigkeiten.</summary>
    public const int MaxEntries = 20;

    public static DataHealth ByAge(TimeSpan age, TimeSpan lateAfter, TimeSpan downAfter) =>
        age > downAfter ? DataHealth.Down
        : age > lateAfter ? DataHealth.Late
        : DataHealth.Current;

    /// <summary>
    /// So viele Calls einer Quelle dürfen scheitern, ohne dass sie als verspätet gilt. Einzelne Fehler pro Lauf sind normal
    /// (Timeout, Punkt ohne Wert), die Prognose entsteht trotzdem (Entscheid Marc, 21.11.2026).
    /// </summary>
    public const double TolerableFailedShare = 0.1;

    /// <summary>Alle Calls gescheitert: gestört. Mehr als <see cref="TolerableFailedShare"/> gescheitert: verspätet.</summary>
    public static DataHealth ByCalls(int calls, int failed) =>
        calls > 0 && failed == calls ? DataHealth.Down
        : calls > 0 && (double)failed / calls > TolerableFailedShare ? DataHealth.Late
        : DataHealth.Current;

    public static DataHealth Worse(DataHealth a, DataHealth b) => (DataHealth)Math.Max((int)a, (int)b);

    /// <summary>Der schlechteste Zustand der Quellen mit Daten. Ohne Daten bei allen Quellen <see cref="DataHealth.Unknown"/>.</summary>
    public static DataHealth Overall(IEnumerable<DataSourceStatus> sources)
    {
        var known = sources.Where(s => s.Health != DataHealth.Unknown).Select(s => s.Health).ToList();
        return known.Count == 0 ? DataHealth.Unknown : known.Max();
    }
}
