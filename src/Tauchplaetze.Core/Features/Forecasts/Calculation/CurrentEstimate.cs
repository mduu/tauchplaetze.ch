using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>
/// Schwellen und Tiefen der Strömung (`forecast.md`, «Strömung»). Startwerte vom 19.10.2026 ohne Messung.
/// Die Strömung ist eine Information und fliesst nicht in den Index, darum hat sie keine eigene <c>ModelVersion</c>.
/// </summary>
internal record CurrentRules
{
    public static readonly CurrentRules Default = new();

    /// <summary>Oberste Tiefe des Tauchbereichs in m.</summary>
    public double MinDepth { get; init; } = 5;

    /// <summary>Unterste Tiefe des Tauchbereichs in m.</summary>
    public double MaxDepth { get; init; } = 30;

    /// <summary>Unter dieser Geschwindigkeit in m/s gilt die Strömung als ruhig.</summary>
    public double NoticeableFrom { get; init; } = 0.05;

    /// <summary>Über dieser Geschwindigkeit in m/s gilt die Strömung als stark.</summary>
    public double StrongAbove { get; init; } = 0.2;

    public CurrentLevel LevelOf(double speed) =>
        speed < NoticeableFrom ? CurrentLevel.Calm : speed > StrongAbove ? CurrentLevel.Strong : CurrentLevel.Noticeable;
}

/// <summary>Die Strömung eines Tages am Platz. <see cref="DirectionToward"/>: Richtung, in die das Wasser fliesst, in Grad (0 = Nord, 90 = Ost).</summary>
internal record DayCurrent(double Speed, double DirectionToward, CurrentLevel Level);

/// <summary>Strömung aus dem 3D-Modell (`u` nach Osten, `v` nach Norden).</summary>
internal static class CurrentEstimate
{
    /// <summary>
    /// Die stärkste Strömung im Tauchfenster des Tages, über den Tauchbereich gemittelt. <c>null</c> ohne Werte.
    /// </summary>
    /// <remarks>
    /// Pro Zeitschritt zählt der Mittelwert der Geschwindigkeiten aller Tiefen im Tauchbereich. So heben sich Strömungen in
    /// entgegengesetzter Richtung nicht auf. Die Richtung kommt aus dem Mittel der Vektoren. Über den Tag gilt der stärkste
    /// Zeitschritt, wie beim Wind: Für einen Tauchgang zählt, wie stark es werden kann.
    /// </remarks>
    public static DayCurrent? For(DateOnly day, DepthTimeSeries? series, DiveWindow window, CurrentRules rules)
    {
        if (series is not { EastVelocity: { } east, NorthVelocity: { } north })
        {
            return null;
        }

        var start = SwissTime.ToUtc(day, window.StartHour);
        var end = SwissTime.ToUtc(day, window.EndHour);
        var depths = Enumerable.Range(0, series.Depths.Count)
            .Where(d => series.Depths[d] >= rules.MinDepth && series.Depths[d] <= rules.MaxDepth)
            .ToList();

        var steps = Enumerable.Range(0, series.Times.Count)
            .Where(t => series.Times[t] >= start && series.Times[t] < end)
            .Select(t => AtTime(t, depths, east, north))
            .OfType<(double Speed, double U, double V)>()
            .ToList();

        if (steps.Count == 0)
        {
            return null;
        }

        var strongest = steps.MaxBy(s => s.Speed);
        return new DayCurrent(strongest.Speed, DirectionToward(strongest.U, strongest.V), rules.LevelOf(strongest.Speed));
    }

    /// <summary>Richtung, in die ein Vektor zeigt, in Grad von Nord im Uhrzeigersinn.</summary>
    public static double DirectionToward(double east, double north) =>
        (Math.Atan2(east, north) * 180 / Math.PI + 360) % 360;

    private static (double Speed, double U, double V)? AtTime(int time, List<int> depths, double?[,] east, double?[,] north)
    {
        var vectors = depths
            .Where(d => east[d, time] is not null && north[d, time] is not null)
            .Select(d => (U: east[d, time]!.Value, V: north[d, time]!.Value))
            .ToList();

        return vectors.Count == 0
            ? null
            : (vectors.Average(v => Math.Sqrt(v.U * v.U + v.V * v.V)), vectors.Average(v => v.U), vectors.Average(v => v.V));
    }
}
