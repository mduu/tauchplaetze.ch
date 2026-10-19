namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Ein Wert des Satelliten an einem Tag.</summary>
internal record SatelliteObservation(DateOnly Date, double Value);

/// <summary>
/// Die geschätzte Klarheit eines Tages. <see cref="SecchiDepth"/> gilt in der Seemitte (Satellitenpunkt), <see cref="ShoreDepth"/> in Ufernähe am Platz.
/// <see cref="ObservedAt"/> bleibt auch bei zu altem Wert gesetzt: Die Seite zeigt das Datum des letzten Bilds.
/// </summary>
internal record ClarityEstimate(
    double? SecchiDepth, DateOnly? ObservedAt, VisibilityIndicator Indicator, double? ShoreDepth = null, int? AgeDays = null, ClaritySource? Source = null)
{
    public static readonly ClarityEstimate Unknown = new(null, null, VisibilityIndicator.Unknown);

    /// <summary>
    /// Wählt die Quelle (`rules-1.2`): Sentinel-2 in Ufernähe, solange es frisch ist (höchstens <see cref="DiveIndexRules.SatelliteFullAgeDays"/> alt).
    /// Sonst der jüngere gültige Wert der beiden, bei gleichem Alter Sentinel-2. Ist keiner gültig, bleibt das Datum des jüngsten Bilds.
    /// </summary>
    public static ClarityEstimate Choose(
        DateOnly day,
        SatelliteObservation? shoreSecchi,
        SatelliteObservation? lakeSecchi,
        Func<DateOnly, double?> rainSince,
        double? dischargeRatio,
        DiveIndexRules rules)
    {
        var shore = shoreSecchi is null
            ? Unknown
            : Estimate(day, shoreSecchi, rainSince(shoreSecchi.Date), dischargeRatio, 1.0, rules) with { Source = ClaritySource.Sentinel2Shore };
        var lake = lakeSecchi is null
            ? Unknown
            : For(day, lakeSecchi, rainSince(lakeSecchi.Date), dischargeRatio, rules) with { Source = ClaritySource.Sentinel3Lake };

        if (shore.ShoreDepth is not null && shore.AgeDays <= rules.SatelliteFullAgeDays)
        {
            return shore;
        }

        var valid = new[] { shore, lake }.Where(c => c.ShoreDepth is not null).OrderBy(c => c.AgeDays).ToList();
        if (valid.Count > 0)
        {
            return valid[0];
        }

        return new[] { shore, lake }.Where(c => c.ObservedAt is not null).OrderByDescending(c => c.ObservedAt).FirstOrDefault() ?? Unknown;
    }

    /// <summary>
    /// Der letzte Satellitenwert, höchstens <see cref="DiveIndexRules.SatelliteMaxAgeDays"/> alt, korrigiert um Regen und Abfluss seit dem Bild,
    /// in Ufernähe mal <see cref="DiveIndexRules.ShoreFactor"/> (`forecast.md`, «Klarheit»). Klarheit ist keine Prognose. Der Sicht-Indikator gilt in Ufernähe.
    /// </summary>
    public static ClarityEstimate For(DateOnly day, SatelliteObservation? secchi, double? rainSinceObservation, double? dischargeRatio, DiveIndexRules rules) =>
        Estimate(day, secchi, rainSinceObservation, dischargeRatio, rules.ShoreFactor, rules);

    private static ClarityEstimate Estimate(
        DateOnly day, SatelliteObservation? secchi, double? rainSinceObservation, double? dischargeRatio, double shoreFactor, DiveIndexRules rules)
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
        var shore = Math.Round(depth * shoreFactor, 1);

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
