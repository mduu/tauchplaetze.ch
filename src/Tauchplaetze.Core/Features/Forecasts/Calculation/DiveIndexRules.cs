namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Ein Stützpunkt: bei <see cref="Input"/> gilt <see cref="Score"/>. Dazwischen linear.</summary>
internal record SupportPoint(double Input, double Score);

/// <summary>
/// Gewichte und Stützpunkte des regelbasierten Index (`forecast.md`, «Der regelbasierte Index»). Jede Änderung erhöht <see cref="ModelVersion"/>.
/// </summary>
/// <remarks>
/// Die Werte für Algen, die Korrektur der Klarheit und die Schichtung sind Startwerte vom 18.10.2026 ohne Messung.
/// `rules-1.1` (18.10.2026): fehlende Klarheit zählt vorsichtig statt gar nicht, alte Satellitenwerte verlieren Gewicht,
/// die Schätzung aus der Seemitte gilt in Ufernähe nur teilweise, und eine trübe Oberfläche deckelt die Schichtung.
/// `rules-1.2` (19.10.2026): Sentinel-2 in Ufernähe ist die Hauptquelle der Klarheit, ohne Uferfaktor. Sentinel-3 bleibt Rückfall.
/// Volle Punkte für die Schichtung erst ab guter Sicht in Ufernähe (6 m).
/// `rules-1.3` (19.10.2026, Entscheid Marc): Mässige Sicht in Ufernähe begrenzt den Index auf 7, schlechte auf 5.
/// `rules-1.4` (20.10.2026, Entscheid Marc): An künftigen Tagen zählt für Zuflüsse und Korrektur der Klarheit der vorhergesagte Abfluss des BAFU.
/// `rules-1.5` (20.10.2026, Entscheid Marc): Ein Zufluss zählt nach der Distanz zu seiner Mündung, der Median mindestens 1 m³/s,
/// am Fluss die Station des Platzes, und viel Wasser im Fluss gibt einen Abzug für die Strömung.
/// `rules-1.6` (21.10.2026, Fehlerbehebung): Die Sprungschicht zählt erst ab 1 m Tiefe und ab 1 m Dicke.
/// Sonst hielt die warme Haut der obersten Dezimeter im 3D-Modell als Sprungschicht «0–0 m» her.
/// Sie werden mit Meldungen und eigenen Tauchgängen geprüft.
/// </remarks>
internal record DiveIndexRules
{
    public static readonly DiveIndexRules Default = new();

    public string ModelVersion { get; init; } = "rules-1.6";

    public double ClarityWeight { get; init; } = 0.35;

    public double WindWeight { get; init; } = 0.25;

    public double InflowWeight { get; init; } = 0.20;

    public double StratificationWeight { get; init; } = 0.20;

    public double ThermoclineGradient { get; init; } = 0.5;

    /// <summary>Punkte darüber zählen nicht für die Sprungschicht, in Metern.</summary>
    public double ThermoclineMinDepth { get; init; } = 1;

    /// <summary>Eine dünnere Sprungschicht gilt als keine, in Metern.</summary>
    public double ThermoclineMinThickness { get; init; } = 1;

    public int SatelliteMaxAgeDays { get; init; } = 14;

    /// <summary>Bis zu diesem Alter in Tagen zählt der Satellitenwert voll. Danach nähern sich die Punkte bis <see cref="SatelliteMaxAgeDays"/> linear <see cref="UnknownClarityScore"/>.</summary>
    public int SatelliteFullAgeDays { get; init; } = 5;

    /// <summary>Tage, über die ein alter Satellitenwert zum vorsichtigen Wert wird. Braucht <see cref="SatelliteFullAgeDays"/> &lt; <see cref="SatelliteMaxAgeDays"/>.</summary>
    public int AgingDays => SatelliteMaxAgeDays > SatelliteFullAgeDays
        ? SatelliteMaxAgeDays - SatelliteFullAgeDays
        : throw new InvalidOperationException($"{nameof(SatelliteFullAgeDays)} muss kleiner sein als {nameof(SatelliteMaxAgeDays)}.");

    /// <summary>Punkte für die Klarheit, wenn kein gültiger Satellitenwert da ist.</summary>
    public double UnknownClarityScore { get; init; } = 4;

    /// <summary>Höchster Index ohne Sichtwert.</summary>
    public double MaxIndexWithoutClarity { get; init; } = 6.5;

    /// <summary>Höchster Index bei mässiger Sicht in Ufernähe (3 bis 6 m).</summary>
    public double MaxIndexWithModerateVisibility { get; init; } = 7;

    /// <summary>Höchster Index bei schlechter Sicht in Ufernähe (unter 3 m).</summary>
    public double MaxIndexWithPoorVisibility { get; init; } = 5;

    /// <summary>Anteil der Secchi-Tiefe aus der Seemitte (Sentinel-3), der in Ufernähe gilt. Sentinel-2 misst schon in Ufernähe.</summary>
    public double ShoreFactor { get; init; } = 0.7;

    /// <summary>Secchi-Tiefe in Ufernähe in m, ab der die Oberfläche als «mässig» gilt.</summary>
    public double SurfaceModerateFrom { get; init; } = 3;

    /// <summary>Secchi-Tiefe in Ufernähe in m, ab der die Oberfläche als «gut» gilt. Erst dann gibt die Schichtung volle Punkte (`rules-1.2`).</summary>
    public double SurfaceGoodFrom { get; init; } = 6;

    /// <summary>Höchste Punkte für die Schichtung, wenn die Oberfläche nicht gut (unter <see cref="SurfaceGoodFrom"/>) oder unbekannt ist.</summary>
    public double MaxStratificationWithTurbidSurface { get; init; } = 7;

    /// <summary>Secchi-Tiefe in m → Punkte.</summary>
    public IReadOnlyList<SupportPoint> Clarity { get; init; } = [new(2, 1), new(10, 10)];

    /// <summary>Wind im Tauchfenster in Bft → Punkte.</summary>
    public IReadOnlyList<SupportPoint> Wind { get; init; } = [new(2, 10), new(5, 1)];

    /// <summary>Regen der 72 h davor in mm → Punkte.</summary>
    public IReadOnlyList<SupportPoint> Rain { get; init; } = [new(5, 10), new(40, 1)];

    /// <summary>Abfluss geteilt durch den Median → Punkte.</summary>
    public IReadOnlyList<SupportPoint> Discharge { get; init; } = [new(1.2, 10), new(3, 1)];

    /// <summary>Am Fluss: Abfluss geteilt durch den Median → Abzug vom Index für die Strömung (`rules-1.5`).</summary>
    public IReadOnlyList<SupportPoint> RiverCurrentPenalty { get; init; } = [new(1.5, 0), new(3, 3)];

    /// <summary>Tiefster Index nach dem Abzug für die Strömung.</summary>
    public double MinIndexAfterCurrentPenalty { get; init; } = 1;

    /// <summary>Untergrenze der Sprungschicht in m → Punkte.</summary>
    public IReadOnlyList<SupportPoint> ThermoclineBottom { get; init; } = [new(20, 10), new(30, 1)];

    /// <summary>Chlorophyll in mg/m³: bis hier wenig Algen.</summary>
    public double AlgaeLowBelow { get; init; } = 4;

    /// <summary>Chlorophyll in mg/m³: ab hier Algenblüte.</summary>
    public double AlgaeHighFrom { get; init; } = 10;

    /// <summary>Regen seit dem Satellitenbild in mm → Anteil der Secchi-Tiefe, der bleibt.</summary>
    public IReadOnlyList<SupportPoint> ClarityAfterRain { get; init; } = [new(5, 1.0), new(30, 0.6)];

    /// <summary>Abfluss geteilt durch den Median → Anteil der Secchi-Tiefe, der bleibt.</summary>
    public IReadOnlyList<SupportPoint> ClarityAfterDischarge { get; init; } = [new(1.2, 1.0), new(3, 0.6)];

    public static double Interpolate(IReadOnlyList<SupportPoint> points, double input)
    {
        if (input <= points[0].Input)
        {
            return points[0].Score;
        }

        if (input >= points[^1].Input)
        {
            return points[^1].Score;
        }

        var upperIndex = Enumerable.Range(1, points.Count - 1).First(i => input <= points[i].Input);
        var lower = points[upperIndex - 1];
        var upper = points[upperIndex];
        return lower.Score + (input - lower.Input) / (upper.Input - lower.Input) * (upper.Score - lower.Score);
    }
}
