namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>Grossregion der Schweiz nach BFS. Die Reihenfolge ist die Reihenfolge auf der Gewässer-Übersicht.</summary>
public enum SwissRegion
{
    /// <summary>Genferseeregion: VD, VS, GE.</summary>
    LakeGeneva,

    /// <summary>Espace Mittelland: BE, FR, SO, NE, JU.</summary>
    Mittelland,

    /// <summary>Nordwestschweiz: BS, BL, AG.</summary>
    Northwest,

    /// <summary>Zürich: ZH.</summary>
    Zurich,

    /// <summary>Ostschweiz: GL, SH, AR, AI, SG, GR, TG.</summary>
    East,

    /// <summary>Zentralschweiz: LU, UR, SZ, OW, NW, ZG.</summary>
    Central,

    /// <summary>Tessin: TI.</summary>
    Ticino,
}

/// <summary>Kantone und ihre Grossregion.</summary>
public static class SwissRegions
{
    private static readonly Dictionary<string, SwissRegion> ByCanton = new()
    {
        ["VD"] = SwissRegion.LakeGeneva, ["VS"] = SwissRegion.LakeGeneva, ["GE"] = SwissRegion.LakeGeneva,
        ["BE"] = SwissRegion.Mittelland, ["FR"] = SwissRegion.Mittelland, ["SO"] = SwissRegion.Mittelland, ["NE"] = SwissRegion.Mittelland, ["JU"] = SwissRegion.Mittelland,
        ["BS"] = SwissRegion.Northwest, ["BL"] = SwissRegion.Northwest, ["AG"] = SwissRegion.Northwest,
        ["ZH"] = SwissRegion.Zurich,
        ["GL"] = SwissRegion.East, ["SH"] = SwissRegion.East, ["AR"] = SwissRegion.East, ["AI"] = SwissRegion.East, ["SG"] = SwissRegion.East, ["GR"] = SwissRegion.East, ["TG"] = SwissRegion.East,
        ["LU"] = SwissRegion.Central, ["UR"] = SwissRegion.Central, ["SZ"] = SwissRegion.Central, ["OW"] = SwissRegion.Central, ["NW"] = SwissRegion.Central, ["ZG"] = SwissRegion.Central,
        ["TI"] = SwissRegion.Ticino,
    };

    /// <summary>Die Region zum Kantonskürzel. Ein unbekanntes Kürzel ergibt <c>null</c>.</summary>
    public static SwissRegion? Of(string? canton) =>
        canton is not null && ByCanton.TryGetValue(canton.Trim().ToUpperInvariant(), out var region) ? region : null;
}
