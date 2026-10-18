namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Ein Wert des Satelliten an einem Tag.</summary>
internal record SatelliteObservation(DateOnly Date, double Value);

/// <summary>
/// Die geschätzte Klarheit eines Tages. <see cref="SecchiDepth"/> gilt in der Seemitte (Satellitenpunkt), <see cref="ShoreDepth"/> in Ufernähe am Platz.
/// <see cref="ObservedAt"/> bleibt auch bei zu altem Wert gesetzt: Die Seite zeigt das Datum des letzten Bilds.
/// </summary>
internal record ClarityEstimate(double? SecchiDepth, DateOnly? ObservedAt, VisibilityIndicator Indicator, double? ShoreDepth = null, int? AgeDays = null)
{
    public static readonly ClarityEstimate Unknown = new(null, null, VisibilityIndicator.Unknown);

    /// <summary>
    /// Der letzte Satellitenwert, höchstens <see cref="DiveIndexRules.SatelliteMaxAgeDays"/> alt, korrigiert um Regen und Abfluss seit dem Bild,
    /// in Ufernähe mal <see cref="DiveIndexRules.ShoreFactor"/> (`forecast.md`, «Klarheit»). Klarheit ist keine Prognose. Der Sicht-Indikator gilt in Ufernähe.
    /// </summary>
    public static ClarityEstimate For(DateOnly day, SatelliteObservation? secchi, double? rainSinceObservation, double? dischargeRatio, DiveIndexRules rules)
    {
        if (secchi is null)
        {
            return Unknown;
        }

        var age = day.DayNumber - secchi.Date.DayNumber;
        if (age > rules.SatelliteMaxAgeDays)
        {
            return new ClarityEstimate(null, secchi.Date, VisibilityIndicator.Unknown);
        }

        var remaining = Math.Min(
            rainSinceObservation is { } rain ? DiveIndexRules.Interpolate(rules.ClarityAfterRain, rain) : 1.0,
            dischargeRatio is { } ratio ? DiveIndexRules.Interpolate(rules.ClarityAfterDischarge, ratio) : 1.0);
        var depth = Math.Round(secchi.Value * remaining, 1);
        var shore = Math.Round(depth * rules.ShoreFactor, 1);

        return new ClarityEstimate(depth, secchi.Date, IndicatorFor(shore), shore, age);
    }

    public static AlgaeLevel AlgaeFor(DateOnly day, SatelliteObservation? chlorophyll, DiveIndexRules rules) => chlorophyll switch
    {
        null => AlgaeLevel.Unknown,
        _ when day.DayNumber - chlorophyll.Date.DayNumber > rules.SatelliteMaxAgeDays => AlgaeLevel.Unknown,
        { Value: var value } when value < rules.AlgaeLowBelow => AlgaeLevel.Low,
        { Value: var value } when value < rules.AlgaeHighFrom => AlgaeLevel.Medium,
        _ => AlgaeLevel.High,
    };

    private static VisibilityIndicator IndicatorFor(double depth) => depth switch
    {
        >= 10 => VisibilityIndicator.VeryGood,
        >= 6 => VisibilityIndicator.Good,
        >= 3 => VisibilityIndicator.Moderate,
        _ => VisibilityIndicator.Poor,
    };
}
