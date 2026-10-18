namespace Tauchplaetze.Core.Features.DiveSites;

/// <summary>
/// Art eines Elements der Tauchskizze (`dive-site.md`, «Tauchskizze»). Die Art legt die Form fest.
/// Die Revision speichert die Zahl, die Tabelle den Namen: Werte und Namen nie ändern, neue Arten nur hinten anhängen.
/// </summary>
public enum SketchKind
{
    /// <summary>Skulptur (Punkt).</summary>
    Sculpture = 0,

    /// <summary>Baumstamm (Punkt).</summary>
    TreeTrunk = 1,

    /// <summary>Wrack (Punkt).</summary>
    Wreck = 2,

    /// <summary>Gegenstand, z.B. Auto, Velo, Tisch (Punkt).</summary>
    Object = 3,

    /// <summary>Felsblock (Punkt).</summary>
    Boulder = 4,

    /// <summary>Highlight mit freiem Namen (Punkt).</summary>
    Highlight = 5,

    /// <summary>Gefahr, z.B. Netz, Strömung, Schiffsverkehr (Punkt).</summary>
    Danger = 6,

    /// <summary>Steilwand (Linie).</summary>
    Wall = 7,

    /// <summary>Kante, Abbruch (Linie).</summary>
    Edge = 8,

    /// <summary>Tauchroute (Linie).</summary>
    Route = 9,

    /// <summary>Leine, z.B. Orientierungsleine (Linie).</summary>
    Line = 10,

    /// <summary>Seegras- oder Pflanzenfeld (Fläche).</summary>
    PlantField = 11,
}

/// <summary>Form eines Skizzenelements.</summary>
public enum SketchShape
{
    /// <summary>Ein Punkt.</summary>
    Point,

    /// <summary>Eine Linie aus mindestens zwei Punkten.</summary>
    Line,

    /// <summary>Eine Fläche aus mindestens drei Punkten.</summary>
    Area,
}

/// <summary>Regeln der Tauchskizze.</summary>
public static class SketchRules
{
    /// <summary>Höchstens so viele Elemente pro Skizze.</summary>
    public const int MaxFeatures = 200;

    /// <summary>Höchstens so viele Punkte pro Linie oder Fläche.</summary>
    public const int MaxPoints = 200;

    /// <summary>Alle Punkte liegen höchstens so weit (m) von der Koordinate des Platzes.</summary>
    public const double MaxDistanceMeters = 2000;

    /// <summary>Länge einer Beschriftung.</summary>
    public const int LabelLength = 80;

    /// <summary>Tiefe in Metern, höchstens.</summary>
    public const int MaxDepth = 200;

    /// <summary>Die Form zur Art.</summary>
    public static SketchShape ShapeOf(this SketchKind kind) => kind switch
    {
        SketchKind.Wall or SketchKind.Edge or SketchKind.Route or SketchKind.Line => SketchShape.Line,
        SketchKind.PlantField => SketchShape.Area,
        _ => SketchShape.Point,
    };
}
