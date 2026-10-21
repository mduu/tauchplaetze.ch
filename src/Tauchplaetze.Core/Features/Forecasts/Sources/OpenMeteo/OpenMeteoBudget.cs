using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

/// <summary>Kontingent von Open-Meteo pro Minute (`data-sources.md`, «Wetter: Open-Meteo»). Ein Punkt eines Calls zählt als ein Call.</summary>
public class OpenMeteoOptions
{
    public const string Section = "Forecast:OpenMeteo";

    /// <summary>Punkte pro Minute. Das Gratis-Kontingent sind 600 Calls pro Minute, wir bleiben darunter.</summary>
    public int PointsPerMinute { get; set; } = 400;
}

/// <summary>Verteilt die Calls an Open-Meteo über die Minute und sperrt sie, wenn das Kontingent der Stunde oder des Tages erschöpft ist.</summary>
internal sealed class OpenMeteoBudget(TimeProvider time, IOptions<OpenMeteoOptions> options)
{
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Queue<(DateTimeOffset At, int Points)> spent = new();

    private readonly SemaphoreSlim gate = new(1, 1);

    private DateTimeOffset blockedUntil = DateTimeOffset.MinValue;

    public bool IsBlocked => time.GetUtcNow() < blockedUntil;

    /// <summary>Wartet, bis <paramref name="points"/> in die laufende Minute passen, und bucht sie.</summary>
    public async Task Take(int points, CancellationToken ct)
    {
        var limit = Math.Max(points, options.Value.PointsPerMinute);
        await gate.WaitAsync(ct);
        try
        {
            while (true)
            {
                var now = time.GetUtcNow();
                while (spent.TryPeek(out var oldest) && oldest.At <= now - Window)
                {
                    spent.Dequeue();
                }

                if (spent.Sum(s => s.Points) + points <= limit)
                {
                    spent.Enqueue((now, points));
                    return;
                }

                await Task.Delay(spent.Peek().At + Window - now, time, ct);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>Keine Calls mehr bis <paramref name="until"/>.</summary>
    public void BlockUntil(DateTimeOffset until) => blockedUntil = until > blockedUntil ? until : blockedUntil;
}
