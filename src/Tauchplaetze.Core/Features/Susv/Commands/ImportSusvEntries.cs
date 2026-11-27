using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Geo;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.CrossCutting.Time;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Susv.Sources;

namespace Tauchplaetze.Core.Features.Susv.Commands;

/// <summary>
/// Übernimmt die Einträge aller Themen des SUSV in Deutsch, Französisch und Italienisch (ADR-013, `stories/002-susv-import-und-moderation.md`, «Der Job»).
/// Ein gescheitertes oder verdächtiges Thema entfernt nichts. Die Moderation bekommt eine Mail, wenn Einträge neu, geändert oder entfernt sind.
/// </summary>
public record ImportSusvEntriesCommand;

/// <summary>
/// Ergebnis eines Laufs. <see cref="Locked"/>: Ein anderer Lauf hielt den Lock, nichts geschah.
/// <see cref="FailedTopics"/>: Themen ohne brauchbare deutsche Antwort. <see cref="Unreadable"/>: Einträge der Quelle, die sich nicht lesen liessen.
/// </summary>
public record ImportSusvEntriesResult(int Entries, int New, int Changed, int Removed, int FailedTopics, int Unreadable = 0, bool Locked = false);

internal class ImportSusvEntriesHandler(
    TauchplaetzeDbContext dbContext,
    SusvClient client,
    IRawDataStore rawData,
    IRunLock runLock,
    IOptions<SusvOptions> options,
    SusvNotifications notifications,
    SusvEnglish english,
    TimeProvider time,
    ILogger<ImportSusvEntriesHandler> logger)
    : ICommandHandler<ImportSusvEntriesCommand, ImportSusvEntriesResult>
{
    public const string LockName = "susv";

    private int calls;

    public async Task<ImportSusvEntriesResult> Handle(ImportSusvEntriesCommand command, CancellationToken ct = default)
    {
        await using var acquired = await runLock.TryAcquire(LockName, ct);
        if (acquired is null)
        {
            return new ImportSusvEntriesResult(0, 0, 0, 0, 0, Locked: true);
        }

        var now = time.GetUtcNow();
        var run = new Run(now, SwissTime.Today(time), new SourceRecorder(rawData, $"susv/{now:yyyy-MM-dd}/{now:HHmm}"));
        var existing = await dbContext.SusvEntries.Include(e => e.Translations).ToListAsync(ct);

        foreach (var (topic, filter) in SusvRules.Filters.OrderBy(f => f.Key))
        {
            await ImportTopic(run, topic, filter, existing.Where(e => e.Topic == topic).ToList(), ct);
        }

        Purge(existing, now);
        await dbContext.SaveChangesAsync(ct);
        await notifications.ImportChanged(run.New, run.Changed, run.Removed, ct);
        await english.Update(null, ct);

        if (run.FailedTopics > 0 || run.Unreadable > 0)
        {
            logger.LogWarning("SUSV import: {FailedTopics} topics failed, {Unreadable} entries unreadable", run.FailedTopics, run.Unreadable);
        }

        return new ImportSusvEntriesResult(run.Seen, run.New.Count, run.Changed.Count, run.Removed.Count, run.FailedTopics, run.Unreadable);
    }

    private async Task ImportTopic(Run run, SusvTopic topic, int filter, List<SusvEntry> known, CancellationToken ct)
    {
        var maps = new Dictionary<string, SusvMap?>();
        foreach (var culture in SusvRules.Cultures)
        {
            maps[culture] = await Call(() => run.Recorder.Record(SusvClient.Source, $"{topic.ToString().ToLowerInvariant()}-{culture}", client.GetMap(culture, filter, ct), ct), ct);
            run.Unreadable += maps[culture]?.Skipped ?? 0;
        }

        var german = maps["de"];
        var active = known.Where(e => e.RemovedAt is null).ToList();
        if (german is null || (german.Entries.Count == 0 && active.Count > 0))
        {
            run.FailedTopics++;
            return;
        }

        foreach (var source in german.Entries)
        {
            var other = SusvRules.Cultures.Skip(1).ToDictionary(c => c, c => maps[c]?.Entries.FirstOrDefault(e => e.Id == source.Id));
            await Upsert(run, topic, source, other, known, ct);
        }

        var delivered = german.Entries.Select(e => e.Id).ToHashSet();
        var missing = active.Where(e => !delivered.Contains(e.SourceId)).ToList();
        if (!SusvRules.MayCountMissing(active.Count, delivered.Count, missing.Count))
        {
            logger.LogWarning("SUSV import: {Topic} lacks {Missing} of {Known} entries, nothing counted as missing", topic, missing.Count, active.Count);
            return;
        }

        foreach (var entry in missing)
        {
            entry.MissedRuns++;
            if (entry.MissedRuns >= SusvRules.MissedRunsUntilRemoved)
            {
                entry.RemovedAt = run.Now;
                if (entry.ReviewStatus != SusvReviewStatus.Ignored)
                {
                    run.Removed.Add(TitleOf(entry));
                }
            }
        }
    }

    private async Task Upsert(Run run, SusvTopic topic, SusvMapEntry german, Dictionary<string, SusvMapEntry?> other, List<SusvEntry> known, CancellationToken ct)
    {
        run.Seen++;
        var entry = known.FirstOrDefault(e => e.SourceId == german.Id);
        var isNew = entry is null;
        if (entry is null)
        {
            entry = new SusvEntry
            {
                Id = Guid.NewGuid(),
                SourceId = german.Id,
                Topic = topic,
                Location = GeoPoints.Create(german.Latitude, german.Longitude),
                ContentHash = "",
                FirstSeenAt = run.Now,
            };
            dbContext.SusvEntries.Add(entry);
            known.Add(entry);
        }

        var refreshAll = SusvRules.HasDetailText(topic) && run.Today.DayOfWeek == DayOfWeek.Monday && entry.TextsFetchedOn != run.Today;
        var listChanged = entry.SourcePublishedAt != german.PublishedAt;
        entry.Location = GeoPoints.Create(german.Latitude, german.Longitude);
        entry.SourcePublishedAt = german.PublishedAt;
        entry.LastSeenAt = run.Now;
        entry.MissedRuns = 0;
        entry.RemovedAt = null;

        var allTexts = true;
        foreach (var (culture, source) in other.Prepend(new KeyValuePair<string, SusvMapEntry?>("de", german)))
        {
            if (source is not null)
            {
                allTexts &= await UpdateTranslation(run, entry, culture, source, listChanged || refreshAll, ct);
            }
        }

        if (refreshAll && allTexts)
        {
            entry.TextsFetchedOn = run.Today;
        }

        var hash = Hash(entry);
        if (isNew)
        {
            run.New.Add(german.Title);
        }
        else if (hash != entry.ContentHash && entry.ReviewStatus == SusvReviewStatus.Confirmed)
        {
            entry.ChangedSinceReview = true;
            run.Changed.Add(german.Title);
        }

        entry.ContentHash = hash;
    }

    /// <summary>
    /// Setzt Titel, Kurztext und Link. Die Detailseite holt sie nur bei einer Einschränkung, und dort nur bei einer neuen Sprache, einer Änderung in der Liste oder am Montag.
    /// Scheitert der Abruf, bleibt der bisherige Text. Ergebnis: Der Text liess sich lesen.
    /// </summary>
    private async Task<bool> UpdateTranslation(Run run, SusvEntry entry, string culture, SusvMapEntry source, bool refresh, CancellationToken ct)
    {
        var translation = entry.Translations.FirstOrDefault(t => t.Culture == culture);
        var created = translation is null;
        if (translation is null)
        {
            translation = new SusvEntryTranslation { EntryId = entry.Id, Culture = culture, Title = source.Title, DetailUrl = source.DetailUrl };
            entry.Translations.Add(translation);
        }

        var changed = refresh || created || translation.Title != source.Title || translation.Teaser != source.Teaser || translation.DetailUrl != source.DetailUrl;
        translation.Title = source.Title;
        translation.Teaser = source.Teaser;
        translation.DetailUrl = source.DetailUrl;
        if (!changed || !SusvRules.HasDetailText(entry.Topic))
        {
            return true;
        }

        var text = await Call(() => run.Recorder.Record(SusvClient.Source, $"detail-{entry.SourceId}-{culture}", client.GetDetail(source.DetailUrl, source.Title, ct), ct, "html"), ct);
        translation.Text = text ?? translation.Text ?? source.Teaser;
        return text is not null;
    }

    private async Task<T?> Call<T>(Func<Task<T?>> call, CancellationToken ct)
        where T : class
    {
        if (calls++ > 0)
        {
            await Task.Delay(options.Value.CallInterval, time, ct);
        }

        return await call();
    }

    /// <summary>Entfernte Einträge verschwinden nach <see cref="SusvRules.RemovedRetention"/> auch aus der Liste der Moderation.</summary>
    /// <summary>Nach dem Abgleich, auf dem Stand im Speicher: Ein Eintrag, der in diesem Lauf wieder auftaucht, bleibt.</summary>
    private void Purge(List<SusvEntry> entries, DateTimeOffset now)
    {
        var limit = now - SusvRules.RemovedRetention;
        dbContext.SusvEntries.RemoveRange(entries.Where(e => e.RemovedAt < limit));
    }

    /// <summary>SHA-256 über Titel, Kurztext und Text aller Sprachen der Quelle und das Datum (`datastructure.md`, `SusvEntry`).</summary>
    private static string Hash(SusvEntry entry)
    {
        var parts = SusvRules.Cultures
            .Select(c => entry.Translations.FirstOrDefault(t => t.Culture == c))
            .SelectMany(t => new[] { t?.Title, t?.Teaser, t?.Text })
            .Append(entry.SourcePublishedAt?.ToString("O"));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\u001f', parts.Select(p => p ?? ""))))).ToLowerInvariant();
    }

    private static string TitleOf(SusvEntry entry) => entry.Translations.FirstOrDefault(t => t.Culture == "de")?.Title ?? entry.SourceId.ToString();

    private sealed class Run(DateTimeOffset now, DateOnly today, SourceRecorder recorder)
    {
        public DateTimeOffset Now => now;

        public DateOnly Today => today;

        public SourceRecorder Recorder => recorder;

        public List<string> New { get; } = [];

        public List<string> Changed { get; } = [];

        public List<string> Removed { get; } = [];

        public int Seen { get; set; }

        public int FailedTopics { get; set; }

        public int Unreadable { get; set; }
    }
}
