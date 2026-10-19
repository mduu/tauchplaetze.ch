namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Geschätzte Sicht als Stufe (`forecast.md`, «Sicht-Indikator»).</summary>
public enum VisibilityIndicator
{
    /// <summary>Kein gültiger Wert.</summary>
    Unknown,

    /// <summary>Schlecht, unter 3 m.</summary>
    Poor,

    /// <summary>Mässig, 3 bis 6 m.</summary>
    Moderate,

    /// <summary>Gut, 6 bis 10 m.</summary>
    Good,

    /// <summary>Sehr gut, ab 10 m.</summary>
    VeryGood,
}

/// <summary>Algen aus dem Chlorophyll des Satelliten.</summary>
public enum AlgaeLevel
{
    /// <summary>Kein gültiger Wert.</summary>
    Unknown,

    /// <summary>Wenig.</summary>
    Low,

    /// <summary>Mittel.</summary>
    Medium,

    /// <summary>Viel, Algenblüte.</summary>
    High,
}

/// <summary>Woher die Klarheit eines Tages kommt (`forecast.md`, «Klarheit»).</summary>
public enum ClaritySource
{
    /// <summary>Sentinel-3 am Satellitenpunkt in der Seemitte, in Ufernähe mit Uferfaktor.</summary>
    Sentinel3Lake,

    /// <summary>Sentinel-2 an einem Punkt in Ufernähe, ohne Uferfaktor.</summary>
    Sentinel2Shore,
}

/// <summary>Woher das Temperaturprofil eines Tages kommt.</summary>
public enum ProfileSource
{
    /// <summary>3D-Modell am Platz.</summary>
    Model3d,

    /// <summary>1D-Modell für den ganzen See.</summary>
    Model1d,

    /// <summary>Messstation am Fluss.</summary>
    RiverStation,

    /// <summary>Messung im See (Phase 1).</summary>
    InSitu,
}

/// <summary>Grund für den Index. Die Oberfläche bildet daraus einen Satz aus den Ressourcen.</summary>
public enum ReasonCode
{
    /// <summary>Wenig Wind im Tauchfenster.</summary>
    CalmLake,

    /// <summary>Viel Wind im Tauchfenster.</summary>
    Windy,

    /// <summary>Klares Wasser laut Satellit.</summary>
    ClearWater,

    /// <summary>Trübes Wasser laut Satellit.</summary>
    TurbidWater,

    /// <summary>Viel Regen oder hoher Abfluss in den Tagen davor.</summary>
    TurbidAfterRain,

    /// <summary>Sprungschicht nicht tief, darunter meist klarer.</summary>
    ClearBelowThermocline,

    /// <summary>Durchmischter See ohne Sprungschicht.</summary>
    MixedLake,

    /// <summary>Viele Algen.</summary>
    AlgaeBloom,

    /// <summary>Kein aktueller Satellitenwert.</summary>
    ClarityUnknown,

    /// <summary>Der Satellitenwert ist mehr als einige Tage alt und zählt nur noch teilweise.</summary>
    ClarityStale,
}

/// <summary>Status eines Laufs des Prognose-Jobs.</summary>
public enum ForecastRunStatus
{
    /// <summary>Läuft.</summary>
    Running,

    /// <summary>Alle Quellen lieferten.</summary>
    Succeeded,

    /// <summary>Mindestens eine Quelle fehlte. Die übrigen Werte sind gespeichert.</summary>
    PartiallySucceeded,

    /// <summary>Der Lauf brach ab.</summary>
    Failed,
}

