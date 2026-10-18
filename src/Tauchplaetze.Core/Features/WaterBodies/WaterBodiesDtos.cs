namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>Ein Gewässer in einer Liste, mit dem Namen in der gewünschten Sprache, Mittelpunkt für die Karte und Zahl der veröffentlichten Plätze.</summary>
public record WaterBodySummary(int Id, string Slug, string Name, WaterBodyType Type, double Latitude, double Longitude, int SiteCount);

/// <summary>Art einer Gruppe auf der Gewässer-Übersicht. Die Reihenfolge ist die Reihenfolge auf der Seite.</summary>
public enum WaterBodyGroupKind
{
    /// <summary>Seen mit mindestens <see cref="WaterBodyGroups.LargeLakeSites"/> Plätzen.</summary>
    LargeLakes,

    /// <summary>Die übrigen Seen einer Grossregion.</summary>
    Region,

    /// <summary>Alle Flüsse.</summary>
    Rivers,

    /// <summary>Seen ohne Platz mit Kanton.</summary>
    Other,
}

/// <summary>Eine Gruppe der Gewässer-Übersicht. <see cref="Region"/> nur bei <see cref="WaterBodyGroupKind.Region"/>.</summary>
public record WaterBodyGroup(WaterBodyGroupKind Kind, SwissRegion? Region, IReadOnlyList<WaterBodySummary> Waters);

/// <summary>Regeln der Gruppierung (`ux.md`, «Gewässer-Übersicht»).</summary>
public static class WaterBodyGroups
{
    /// <summary>Ein See mit so vielen veröffentlichten Plätzen gehört zu den grossen Seen.</summary>
    public const int LargeLakeSites = 8;
}
