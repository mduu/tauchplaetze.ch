namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Alles, was der Index für einen Platz und einen Tag braucht.</summary>
internal record DayInputs(
    DayWeather Weather,
    DayProfile? Profile,
    ClarityEstimate Clarity,
    AlgaeLevel Algae,
    double? DischargeRatio,
    bool IsRiver);

/// <summary>Die Teilwerte 1 bis 10. <c>null</c>, wenn die Daten fehlen.</summary>
internal record SubScores(double? Clarity, double? Wind, double? Inflow, double? Stratification);

/// <summary>Index und Begründung eines Tages. <see cref="DiveIndex"/> ist <c>null</c> ohne Wind oder ohne Wasserdaten (Ausblick).</summary>
internal record DayResult(
    double? DiveIndex,
    SubScores Scores,
    Thermocline? Thermocline,
    double? VisibilityGoodBelow,
    IReadOnlyList<ReasonCode> Reasons);

/// <summary>Der regelbasierte Tauch-Index (`forecast.md`, `rules-1.0`, ADR-004).</summary>
internal static class DiveIndexCalculator
{
    public static DayResult Calculate(DayInputs inputs, DiveIndexRules rules)
    {
        var thermocline = inputs.Profile is { } profile ? ThermoclineDetector.Detect(profile.Profile, rules.ThermoclineGradient) : null;
        var clarity = ClarityScore(inputs, rules);
        var scores = new SubScores(
            clarity,
            inputs.Weather.WindBeaufort is { } bft ? DiveIndexRules.Interpolate(rules.Wind, bft) : null,
            InflowScore(inputs, rules),
            StratificationScore(inputs, thermocline, clarity, rules));

        var hasWaterData = scores.Clarity is not null || inputs.Profile is not null;
        double? index = scores.Wind is not null && hasWaterData ? WeightedMean(scores, rules) : null;

        return new DayResult(index, scores, thermocline, thermocline?.Bottom, Reasons(inputs, thermocline));
    }

    private static double? ClarityScore(DayInputs inputs, DiveIndexRules rules)
    {
        if (inputs.Clarity.SecchiDepth is not { } depth)
        {
            return null;
        }

        var score = DiveIndexRules.Interpolate(rules.Clarity, depth);
        return inputs.Algae switch
        {
            AlgaeLevel.High => Math.Min(score, 3),
            AlgaeLevel.Medium => Math.Max(score - 2, 1),
            _ => score,
        };
    }

    private static double? InflowScore(DayInputs inputs, DiveIndexRules rules)
    {
        double?[] scores =
        [
            inputs.Weather.PrecipitationLast72h is { } rain ? DiveIndexRules.Interpolate(rules.Rain, rain) : null,
            inputs.DischargeRatio is { } ratio ? DiveIndexRules.Interpolate(rules.Discharge, ratio) : null,
        ];
        var present = scores.OfType<double>().ToList();
        return present.Count > 0 ? present.Min() : null;
    }

    private static double? StratificationScore(DayInputs inputs, Thermocline? thermocline, double? clarity, DiveIndexRules rules)
    {
        if (inputs.IsRiver || inputs.Profile is null)
        {
            return null;
        }

        if (thermocline is null)
        {
            return inputs.Algae switch
            {
                AlgaeLevel.High => 3,
                AlgaeLevel.Medium => 6,
                _ => 10,
            };
        }

        var score = DiveIndexRules.Interpolate(rules.ThermoclineBottom, thermocline.Bottom);
        return clarity >= 7 ? Math.Max(score, 6) : score;
    }

    private static double WeightedMean(SubScores scores, DiveIndexRules rules)
    {
        (double? Score, double Weight)[] parts =
        [
            (scores.Clarity, rules.ClarityWeight),
            (scores.Wind, rules.WindWeight),
            (scores.Inflow, rules.InflowWeight),
            (scores.Stratification, rules.StratificationWeight),
        ];
        var present = parts.Where(p => p.Score is not null).ToList();
        var sum = present.Sum(p => p.Score!.Value * p.Weight) / present.Sum(p => p.Weight);
        return Math.Round(Math.Clamp(sum, 0, 10), 1);
    }

    private static IReadOnlyList<ReasonCode> Reasons(DayInputs inputs, Thermocline? thermocline)
    {
        var reasons = new List<ReasonCode>();
        var bft = inputs.Weather.WindBeaufort;
        var secchi = inputs.Clarity.SecchiDepth;

        Add(reasons, ReasonCode.AlgaeBloom, inputs.Algae == AlgaeLevel.High);
        Add(reasons, ReasonCode.TurbidAfterRain, inputs.Weather.PrecipitationLast72h >= 20 || inputs.DischargeRatio >= 2);
        Add(reasons, ReasonCode.Windy, bft >= 4);
        Add(reasons, ReasonCode.TurbidWater, secchi < 4);
        Add(reasons, ReasonCode.ClarityUnknown, secchi is null && !inputs.IsRiver);
        Add(reasons, ReasonCode.ClearBelowThermocline, thermocline?.Bottom <= 20);
        Add(reasons, ReasonCode.ClearWater, secchi >= 8);
        Add(reasons, ReasonCode.CalmLake, bft <= 2);
        Add(reasons, ReasonCode.MixedLake, inputs.Profile is not null && thermocline is null && !inputs.IsRiver);

        return reasons;
    }

    private static void Add(List<ReasonCode> reasons, ReasonCode reason, bool applies)
    {
        if (applies)
        {
            reasons.Add(reason);
        }
    }
}
