namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Alles, was der Index für einen Platz und einen Tag braucht.</summary>
internal record DayInputs(
    DayWeather Weather,
    DayProfile? Profile,
    ClarityEstimate Clarity,
    AlgaeLevel Algae,
    double? DischargeRatio,
    bool IsRiver,
    KnownThermocline? Thermocline = null);

/// <summary>Die gespeicherte Sprungschicht eines Tages. Die Neuberechnung hat nur noch vier Punkte des Profils, nicht das ganze.</summary>
internal record KnownThermocline(Thermocline? Value);

/// <summary>Die Teilwerte 1 bis 10. Die Klarheit hat immer einen Wert (ohne Satellit vorsichtig), Wind, Zuflüsse und Schichtung sind <c>null</c> ohne Daten.</summary>
internal record SubScores(double? Clarity, double? Wind, double? Inflow, double? Stratification);

/// <summary>Index und Begründung eines Tages. <see cref="DiveIndex"/> ist <c>null</c> ohne Wind oder ohne Wasserdaten (Ausblick).</summary>
internal record DayResult(
    double? DiveIndex,
    SubScores Scores,
    Thermocline? Thermocline,
    double? VisibilityGoodBelow,
    IReadOnlyList<ReasonCode> Reasons);

/// <summary>Der regelbasierte Tauch-Index (`forecast.md`, `rules-1.5`, ADR-004).</summary>
internal static class DiveIndexCalculator
{
    public static DayResult Calculate(DayInputs inputs, DiveIndexRules rules)
    {
        var thermocline = inputs.Thermocline is { } known
            ? known.Value
            : inputs.Profile is { } profile ? ThermoclineDetector.Detect(profile.Profile, rules.ThermoclineGradient) : null;
        var clarity = ClarityScore(inputs, rules);
        var scores = new SubScores(
            clarity,
            inputs.Weather.WindBeaufort is { } bft ? DiveIndexRules.Interpolate(rules.Wind, bft) : null,
            InflowScore(inputs, rules),
            StratificationScore(inputs, thermocline, clarity, rules));

        var hasWaterData = inputs.Clarity.ShoreDepth is not null || inputs.Profile is not null;
        double? uncapped = scores.Wind is not null && hasWaterData ? Capped(WeightedMean(scores, rules), inputs, rules) : null;
        var capped = uncapped is { } value ? Math.Min(value, VisibilityCap(inputs.Clarity.Indicator, rules)) : (double?)null;
        var penalty = CurrentPenalty(inputs, rules);
        var index = capped is { } beforePenalty && penalty > 0
            ? Math.Round(Math.Min(beforePenalty, Math.Max(beforePenalty - penalty, rules.MinIndexAfterCurrentPenalty)), 1)
            : capped;

        var reasons = Reasons(inputs, thermocline, rules);
        if (capped < uncapped)
        {
            reasons = [ReasonCode.VisibilityLimitsIndex, .. reasons];
        }

        if (penalty > 0)
        {
            reasons = [ReasonCode.StrongCurrent, .. reasons];
        }

        return new DayResult(index, scores, thermocline, thermocline?.Bottom, reasons);
    }

    /// <remarks>Ohne gültigen Satellitenwert zählt die Klarheit vorsichtig, statt ihr Gewicht den anderen Teilwerten zu geben. Ein Fluss ohne Sichtwert ebenso.</remarks>
    private static double ClarityScore(DayInputs inputs, DiveIndexRules rules)
    {
        if (inputs.Clarity.ShoreDepth is not { } depth)
        {
            return rules.UnknownClarityScore;
        }

        var score = DiveIndexRules.Interpolate(rules.Clarity, depth);
        score = inputs.Algae switch
        {
            AlgaeLevel.High => Math.Min(score, 3),
            AlgaeLevel.Medium => Math.Max(score - 2, 1),
            _ => score,
        };

        return Aged(score, inputs.Clarity.AgeDays ?? 0, rules);
    }

    /// <summary>Ein alter Wert nähert sich linear dem vorsichtigen Wert. Ein schlechter Wert wird durch das Alter nicht besser.</summary>
    private static double Aged(double score, int ageDays, DiveIndexRules rules)
    {
        if (ageDays <= rules.SatelliteFullAgeDays || score <= rules.UnknownClarityScore)
        {
            return score;
        }

        var share = Math.Min(1, (double)(ageDays - rules.SatelliteFullAgeDays) / rules.AgingDays);
        return score - (score - rules.UnknownClarityScore) * share;
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

    private static double? StratificationScore(DayInputs inputs, Thermocline? thermocline, double clarity, DiveIndexRules rules)
    {
        if (inputs.IsRiver || inputs.Profile is null)
        {
            return null;
        }

        double score;
        if (thermocline is null)
        {
            score = inputs.Algae switch
            {
                AlgaeLevel.High => 3,
                AlgaeLevel.Medium => 6,
                _ => 10,
            };
        }
        else
        {
            score = DiveIndexRules.Interpolate(rules.ThermoclineBottom, thermocline.Bottom);
            score = clarity >= 7 ? Math.Max(score, 6) : score;
        }

        return SurfaceIsGood(inputs, rules) ? score : Math.Min(score, rules.MaxStratificationWithTurbidSurface);
    }

    /// <summary>
    /// Volle Punkte für die Tiefe nur, wenn auch der Abstieg durch die Oberfläche gut ist (ab 6 m in Ufernähe), und zwar laut einem frischen Satellitenwert.
    /// «Oben naja, unten ok» gibt keine vollen Punkte (Marc, 19.10.2026).
    /// </summary>
    private static bool SurfaceIsGood(DayInputs inputs, DiveIndexRules rules) =>
        inputs.Clarity.ShoreDepth >= rules.SurfaceGoodFrom && HasFreshClarity(inputs, rules);

    /// <summary>Für die Begründung «unten klarer»: Die Oberfläche ist laut frischem Wert mindestens mässig.</summary>
    private static bool SurfaceIsModerate(DayInputs inputs, DiveIndexRules rules) =>
        inputs.Clarity.ShoreDepth >= rules.SurfaceModerateFrom && HasFreshClarity(inputs, rules);

    private static bool HasFreshClarity(DayInputs inputs, DiveIndexRules rules) =>
        inputs.Clarity is { ShoreDepth: not null, AgeDays: var age } && (age ?? 0) <= rules.SatelliteFullAgeDays;

    /// <remarks>Die Obergrenze gilt, sobald der Satellitenwert altert. So springt der Index am Tag, an dem der Wert unbekannt wird, nicht nach oben.</remarks>
    private static double Capped(double index, DayInputs inputs, DiveIndexRules rules) =>
        HasFreshClarity(inputs, rules) ? index : Math.Min(index, rules.MaxIndexWithoutClarity);

    /// <summary>Gutes Wetter macht trübes Wasser nicht zu einem Top-Tag (`rules-1.3`).</summary>
    private static double VisibilityCap(VisibilityIndicator indicator, DiveIndexRules rules) => indicator switch
    {
        VisibilityIndicator.Poor => rules.MaxIndexWithPoorVisibility,
        VisibilityIndicator.Moderate => rules.MaxIndexWithModerateVisibility,
        _ => 10,
    };

    /// <summary>Am Fluss heisst viel Wasser auch starke Strömung. Der Abfluss zählt so doppelt: als Zufluss und als Abzug.</summary>
    private static double CurrentPenalty(DayInputs inputs, DiveIndexRules rules) =>
        inputs is { IsRiver: true, DischargeRatio: { } ratio } ? DiveIndexRules.Interpolate(rules.RiverCurrentPenalty, ratio) : 0;

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

    private static IReadOnlyList<ReasonCode> Reasons(DayInputs inputs, Thermocline? thermocline, DiveIndexRules rules)
    {
        var reasons = new List<ReasonCode>();
        var bft = inputs.Weather.WindBeaufort;
        var secchi = inputs.Clarity.ShoreDepth;

        Add(reasons, ReasonCode.AlgaeBloom, inputs.Algae == AlgaeLevel.High);
        Add(reasons, ReasonCode.TurbidAfterRain, inputs.Weather.PrecipitationLast72h >= 20 || inputs.DischargeRatio >= 2);
        Add(reasons, ReasonCode.Windy, bft >= 4);
        Add(reasons, ReasonCode.TurbidWater, secchi < 4);
        Add(reasons, ReasonCode.ClarityUnknown, secchi is null && !inputs.IsRiver);
        Add(reasons, ReasonCode.ClarityStale, secchi is not null && inputs.Clarity.AgeDays > rules.SatelliteFullAgeDays);
        Add(reasons, ReasonCode.ClearBelowThermocline, thermocline?.Bottom <= 20 && SurfaceIsModerate(inputs, rules));
        Add(reasons, ReasonCode.ClearWater, secchi >= 8);
        Add(reasons, ReasonCode.CalmLake, bft <= 2 && !inputs.IsRiver);
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
