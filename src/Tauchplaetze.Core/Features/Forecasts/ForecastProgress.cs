using System.Collections.Concurrent;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>
/// Fortschritt eines laufenden Prognose-Laufs, nur im Speicher dieses Prozesses. Die Plattform zeigt ihn an, solange der Lauf läuft.
/// Gespeichert werden die Calls erst am Ende des Laufs (<c>ForecastRun.SourceStatus</c>).
/// </summary>
internal sealed class ForecastProgress
{
    private readonly ConcurrentDictionary<Guid, RunProgress> runs = new();

    public void Start(Guid runId, int sites) => runs[runId] = new RunProgress(sites, 0, 0);

    public void SiteDone(Guid runId, int calls) =>
        runs.AddOrUpdate(runId, _ => new RunProgress(0, 1, calls), (_, p) => p with { SitesDone = p.SitesDone + 1, Calls = calls });

    public void Finish(Guid runId) => runs.TryRemove(runId, out _);

    public RunProgress? Of(Guid runId) => runs.GetValueOrDefault(runId);
}

/// <summary>Plätze des Laufs, davon fertig, und die Calls bisher.</summary>
public record RunProgress(int Sites, int SitesDone, int Calls);
