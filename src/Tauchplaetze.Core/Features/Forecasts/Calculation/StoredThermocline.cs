namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Prüft eine gespeicherte Sprungschicht beim Lesen mit derselben Regel wie beim Rechnen (`rules-1.6`).</summary>
internal static class StoredThermocline
{
    /// <summary>Die Sprungschicht, oder <c>null</c>, wenn sie an der Oberfläche liegt oder zu dünn ist.</summary>
    public static Thermocline? Read(decimal? top, decimal? bottom)
    {
        var rules = DiveIndexRules.Default;
        return top is { } t && bottom is { } b
            ? new Thermocline((double)t, (double)b).Plausible(rules.ThermoclineMinDepth, rules.ThermoclineMinThickness)
            : null;
    }

    /// <summary>Ohne plausible Sprungschicht gibt es weder «besser ab» noch «unten klarer».</summary>
    public static IReadOnlyList<ReasonCode> Reasons(IReadOnlyList<ReasonCode> reasons, Thermocline? thermocline) =>
        thermocline is null ? [.. reasons.Where(r => r != ReasonCode.ClearBelowThermocline)] : reasons;
}
