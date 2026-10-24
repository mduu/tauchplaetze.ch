using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts.Queries;

/// <summary>Die Zahlen der aktuellen Regel für die Seite «Über». So beschreibt die Seite, was der Lauf wirklich rechnet.</summary>
public record GetForecastRulesQuery;

/// <summary>Gewichte in Prozent, Distanzen in m bzw. km, Abfluss als Vielfaches des Medians.</summary>
public record ForecastRulesSummary(
    string ModelVersion,
    int ClarityWeight,
    int WindWeight,
    int InflowWeight,
    int StratificationWeight,
    double MaxIndexWithModerateVisibility,
    double MaxIndexWithPoorVisibility,
    double MaxIndexWithoutClarity,
    double SurfaceModerateFrom,
    double SurfaceGoodFrom,
    int ShoreNearMeters,
    int ShoreFarMeters,
    int ShoreFactor,
    int SatelliteFullAgeDays,
    int SatelliteMaxAgeDays,
    int ClarityMaxLoss,
    int DiveWindowStart,
    int DiveWindowEnd,
    double WindFullUpToBeaufort,
    double WindNoneFromBeaufort,
    int RainHours,
    double InflowFullUpToKm,
    double InflowNoneFromKm,
    decimal MinMedian,
    double CurrentPenaltyFrom,
    double CurrentPenaltyFullAt,
    double CurrentPenaltyMax,
    double CurrentMinDepth,
    double CurrentMaxDepth,
    int ForecastDays,
    IReadOnlyList<TimeOnly> RunTimes,
    double MaxDischargeAgeHours = 6);

internal class GetForecastRulesHandler(IOptions<ForecastSchedule> schedule) : IQueryHandler<GetForecastRulesQuery, ForecastRulesSummary>
{
    private const int RainHours = 72;

    public Task<ForecastRulesSummary> Handle(GetForecastRulesQuery query, CancellationToken ct = default)
    {
        var rules = DiveIndexRules.Default;
        var current = CurrentRules.Default;
        return Task.FromResult(new ForecastRulesSummary(
            rules.ModelVersion,
            Percent(rules.ClarityWeight),
            Percent(rules.WindWeight),
            Percent(rules.InflowWeight),
            Percent(rules.StratificationWeight),
            rules.MaxIndexWithModerateVisibility,
            rules.MaxIndexWithPoorVisibility,
            rules.MaxIndexWithoutClarity,
            rules.SurfaceModerateFrom,
            rules.SurfaceGoodFrom,
            (int)ShorePoint.NearMeters,
            (int)ShorePoint.FarMeters,
            Percent(rules.ShoreFactor),
            rules.SatelliteFullAgeDays,
            rules.SatelliteMaxAgeDays,
            Percent(1 - Math.Min(rules.ClarityAfterRain[^1].Score, rules.ClarityAfterDischarge[^1].Score)),
            DiveWindow.Default.StartHour,
            DiveWindow.Default.EndHour,
            rules.Wind[0].Input,
            rules.Wind[^1].Input,
            RainHours,
            InflowWeights.FullUpToKm,
            InflowWeights.NoneFromKm,
            DischargeRules.MinMedian,
            rules.RiverCurrentPenalty[0].Input,
            rules.RiverCurrentPenalty[^1].Input,
            rules.RiverCurrentPenalty[^1].Score,
            current.MinDepth,
            current.MaxDepth,
            ForecastSources.ForecastDays,
            schedule.Value.Times,
            rules.MaxDischargeAgeHours));
    }

    private static int Percent(double share) => (int)Math.Round(share * 100);
}
