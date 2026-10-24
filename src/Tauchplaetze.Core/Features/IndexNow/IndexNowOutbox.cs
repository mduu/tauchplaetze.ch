namespace Tauchplaetze.Core.Features.IndexNow;

/// <summary>
/// Gesammelte Änderungen an öffentlichen Inhalten bis zur nächsten Meldung. Nur im Speicher: Geht beim Neustart etwas verloren,
/// findet die Suchmaschine die Seite trotzdem über die Sitemap. Eine Tabelle oder Queue wäre für diesen Komfort zu viel.
/// </summary>
internal class IndexNowOutbox
{
    private readonly Lock gate = new();
    private readonly Dictionary<string, int> diveSites = [];
    private readonly Dictionary<int, int> waterBodies = [];

    /// <summary>Ein Platz hat sich geändert. Ein früherer Slug und das frühere Gewässer kommen mit, damit auch deren Seiten neu gelesen werden.</summary>
    public void DiveSiteChanged(string slug, string? oldSlug, int waterBodyId, int? oldWaterBodyId)
    {
        lock (gate)
        {
            diveSites.TryAdd(slug, 0);
            if (oldSlug is not null && oldSlug != slug)
            {
                diveSites.TryAdd(oldSlug, 0);
            }

            waterBodies.TryAdd(waterBodyId, 0);
            if (oldWaterBodyId is { } old && old != waterBodyId)
            {
                waterBodies.TryAdd(old, 0);
            }
        }
    }

    /// <summary>Nimmt alles Gesammelte heraus. Die Zahl ist die Zahl der bisherigen Versuche.</summary>
    public IndexNowBatch Take()
    {
        lock (gate)
        {
            var batch = new IndexNowBatch(new Dictionary<string, int>(diveSites), new Dictionary<int, int>(waterBodies));
            diveSites.Clear();
            waterBodies.Clear();
            return batch;
        }
    }

    /// <summary>Legt eine gescheiterte Meldung für das nächste Intervall zurück, solange Versuche übrig sind.</summary>
    public void Retry(IndexNowBatch batch, int maxAttempts)
    {
        lock (gate)
        {
            foreach (var (slug, attempts) in batch.DiveSites.Where(e => e.Value + 1 < maxAttempts))
            {
                diveSites.TryAdd(slug, attempts + 1);
            }

            foreach (var (id, attempts) in batch.WaterBodies.Where(e => e.Value + 1 < maxAttempts))
            {
                waterBodies.TryAdd(id, attempts + 1);
            }
        }
    }
}

internal record IndexNowBatch(IReadOnlyDictionary<string, int> DiveSites, IReadOnlyDictionary<int, int> WaterBodies)
{
    public bool IsEmpty => DiveSites.Count == 0 && WaterBodies.Count == 0;
}
