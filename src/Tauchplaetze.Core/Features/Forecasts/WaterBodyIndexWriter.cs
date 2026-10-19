using Microsoft.EntityFrameworkCore;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.DiveSites;
using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>
/// Schreibt den Median pro Gewässer und Tag aus den gespeicherten Tagesprognosen (Upsert). Ein zweiter Aufruf für dieselben Tage ändert nichts.
/// Hat an einem Tag kein Platz eines Gewässers mehr einen Index, fällt die Zeile weg.
/// </summary>
internal class WaterBodyIndexWriter(TauchplaetzeDbContext dbContext)
{
    public async Task Write(IReadOnlyCollection<DateOnly> dates, CancellationToken ct)
    {
        if (dates.Count == 0)
        {
            return;
        }

        var forecasts = await dbContext.DailyForecasts
            .Where(f => dates.Contains(f.Date) && f.DiveIndex != null)
            .Join(dbContext.DiveSites.Where(s => s.Status == DiveSiteStatus.Published), f => f.DiveSiteId, s => s.Id,
                (f, s) => new { s.WaterBodyId, f.Date, Index = f.DiveIndex!.Value, f.ModelVersion })
            .ToListAsync(ct);
        var existing = await dbContext.WaterBodyDailyIndexes
            .Where(i => dates.Contains(i.Date))
            .ToDictionaryAsync(i => (i.WaterBodyId, i.Date), ct);

        var medians = forecasts
            .GroupBy(f => (f.WaterBodyId, f.Date))
            .ToDictionary(
                g => g.Key,
                g => (Median: History.Median(g.Select(f => f.Index).ToList()), Sites: g.Count(), ModelVersion: g.Max(f => f.ModelVersion)!));

        foreach (var (key, value) in medians)
        {
            if (!existing.TryGetValue(key, out var row))
            {
                row = new WaterBodyDailyIndex { WaterBodyId = key.WaterBodyId, Date = key.Date, ModelVersion = value.ModelVersion };
                dbContext.WaterBodyDailyIndexes.Add(row);
            }

            row.MedianIndex = value.Median;
            row.Sites = value.Sites;
            row.ModelVersion = value.ModelVersion;
        }

        dbContext.WaterBodyDailyIndexes.RemoveRange(existing.Where(e => !medians.ContainsKey(e.Key)).Select(e => e.Value));
        await dbContext.SaveChangesAsync(ct);
    }
}
