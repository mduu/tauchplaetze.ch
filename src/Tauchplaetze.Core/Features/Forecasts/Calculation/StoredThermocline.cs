namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Prüft eine gespeicherte Sprungschicht beim Lesen mit derselben Regel wie beim Rechnen (`rules-1.6`, `rules-1.7`).</summary>
internal static class StoredThermocline
{
    /// <summary>Die Sprungschicht, oder <c>null</c>, wenn sie fehlt, an der Oberfläche liegt oder zu dünn ist.</summary>
    public static Thermocline? Read(decimal? top, decimal? bottom) => Find(top, bottom).Layer;

    /// <summary>Gespeichert, aber nicht plausibel: «nicht bestimmt» statt «durchmischt».</summary>
    public static ThermoclineFinding Find(decimal? top, decimal? bottom)
    {
        var rules = DiveIndexRules.Default;
        return ThermoclineFinding.Of(
            top is { } t && bottom is { } b ? new Thermocline((double)t, (double)b) : null,
            rules.ThermoclineMinDepth,
            rules.ThermoclineMinThickness);
    }

    /// <summary>Ohne plausible Sprungschicht gibt es weder «besser ab» noch «unten klarer». Nicht bestimmt heisst auch nicht «durchmischt».</summary>
    public static IReadOnlyList<ReasonCode> Reasons(IReadOnlyList<ReasonCode> reasons, ThermoclineFinding finding) =>
        [.. reasons.Where(r => (r != ReasonCode.ClearBelowThermocline || finding.Layer is not null) && (r != ReasonCode.MixedLake || !finding.Undetermined))];
}
