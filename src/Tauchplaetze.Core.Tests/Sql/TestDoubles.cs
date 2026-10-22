using System.Collections.Concurrent;
using System.Net;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Mail.Queue;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.Features.Translations;
using Tauchplaetze.Core.Tests.Features.Forecasts;

namespace Tauchplaetze.Core.Tests.Sql;

/// <summary>Eine feste Uhr. Die Testdaten der Quellen stammen vom 17.10.2026.</summary>
internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public static readonly DateTimeOffset SpikeDay = new(2026, 10, 17, 10, 0, 0, TimeSpan.Zero);

    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class InMemoryRawDataStore : IRawDataStore
{
    public ConcurrentDictionary<string, string> Files { get; } = new();

    public Task Save(string path, string content, CancellationToken ct)
    {
        Files[path] = content;
        return Task.CompletedTask;
    }
}

internal sealed class InMemoryRunLock : IRunLock
{
    private readonly ConcurrentDictionary<string, byte> held = new();

    public Task<IAsyncDisposable?> TryAcquire(string name, CancellationToken ct) =>
        Task.FromResult<IAsyncDisposable?>(held.TryAdd(name, 0) ? new Release(() => held.TryRemove(name, out _)) : null);

    private sealed class Release(Action release) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            release();
            return ValueTask.CompletedTask;
        }
    }
}

/// <summary>Antwortet mit den echten Antworten aus `TestData`. Unbekannte Anfragen ergeben 404. <see cref="Failing"/> lässt eine Quelle scheitern.</summary>
internal sealed class FakeSources : HttpMessageHandler
{
    private static readonly (string UrlPart, string Source, string File)[] Routes =
    [
        ("simulations/depthtime/", "alplakes", "depthtime-delft3d-flow-zurich.json"),
        ("simulations/1d/depthtime/", "alplakes", "depthtime-1d-simstrat-upperzurich.json"),
        ("/Zsd_lee/", "alplakes", "rs-timeseries-zurich-sentinel3-Zsd_lee-w1.json"),
        ("/chla/", "alplakes", "rs-timeseries-zurich-sentinel3-chla-w1.json"),
        ("/Z490/47.2846/8.6080/", "alplakes", "rs-timeseries-zurich-sentinel2-Z490-50m-w2.json"),
        ("/Z490/", "alplakes", "rs-timeseries-zurich-sentinel2-Z490-w2.json"),
        ("remotesensing/metadata", "alplakes", "rs-metadata-summary.json"),
        ("models=meteoswiss_icon_ch1", "open-meteo", "forecast-meteoswiss_icon_ch1.json"),
        ("models=meteoswiss_icon_ch2", "open-meteo", "forecast-meteoswiss_icon_ch2.json"),
        ("models=icon_seamless", "open-meteo", "forecast-icon_seamless.json"),
        ("parameters=flow", "hydro", "daterange-2104-flow.json"),
    ];

    public ConcurrentBag<string> Requests { get; } = [];

    public string? Failing { get; set; }

    /// <summary>Läuft bei jeder Anfrage, etwa um einen Lauf mitten im Abruf abzubrechen.</summary>
    public Action<string>? OnRequest { get; set; }

    /// <summary>Statuscodes, die für einen Teil der URL statt einer Antwort gelten, etwa 503 für einen Ausfall.</summary>
    public ConcurrentDictionary<string, HttpStatusCode> Statuses { get; } = new();

    /// <summary>Antworten, die für einen Teil der URL statt der Datei aus `TestData` gelten.</summary>
    public ConcurrentDictionary<string, string> Overrides { get; } = new();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);
        OnRequest?.Invoke(url);

        if (Statuses.FirstOrDefault(o => url.Contains(o.Key, StringComparison.Ordinal)) is { Key: not null, Value: var status })
        {
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("unavailable") });
        }

        if (Overrides.FirstOrDefault(o => url.Contains(o.Key, StringComparison.Ordinal)) is { Value: { } body })
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) });
        }

        var route = Routes.FirstOrDefault(r => url.Contains(r.UrlPart, StringComparison.Ordinal));
        if (route.File is null || (Failing is { } failing && url.Contains(failing, StringComparison.Ordinal)))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found") });
        }

        var answer = TestData.Read(route.Source, route.File);
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(ForEachPoint(url, answer)) });
    }

    /// <summary>Open-Meteo antwortet bei mehreren Punkten mit einer Liste: dieselbe Antwort pro Punkt.</summary>
    private static string ForEachPoint(string url, string body)
    {
        var query = System.Web.HttpUtility.ParseQueryString(new Uri(url).Query);
        var points = query["latitude"]?.Split(',').Length ?? 1;
        return points > 1 ? $"[{string.Join(',', Enumerable.Repeat(body, points))}]" : body;
    }
}

internal sealed class InMemoryMailSender : IMailSender
{
    public ConcurrentQueue<MailMessage> Sent { get; } = new();

    public Task Send(MailMessage message, CancellationToken ct)
    {
        Sent.Enqueue(message);
        return Task.CompletedTask;
    }

    /// <summary>Der Link des Knopfs in der letzten Mail an <paramref name="to"/>: der erste Link im Text, vor dem Fuss.</summary>
    public string LinkTo(string to) =>
        Sent.Last(m => m.To == to).Body.Split('\n').First(line => line.StartsWith("https://", StringComparison.Ordinal)).Trim();
}

/// <summary>Mail-Queue im Speicher. Sichtbarkeit und Versuche wie bei Storage Queues, die Zeit kommt von <see cref="FixedTime"/>.</summary>
internal sealed class InMemoryMailQueue(TimeProvider time) : IMailQueue
{
    private readonly List<Entry> main = [];

    private readonly List<Entry> dead = [];

    public bool Failing { get; set; }

    /// <summary>Löschen scheitert wie bei einem abgelaufenen Pop Receipt.</summary>
    public bool DeleteFails { get; set; }

    public IReadOnlyList<string> Waiting => [.. main.Select(e => e.Body)];

    public IReadOnlyList<string> DeadLetters => [.. dead.Select(e => e.Body)];

    /// <summary>Wann die wartenden Mails wieder sichtbar werden.</summary>
    public IReadOnlyList<DateTimeOffset> VisibleAt => [.. main.Select(e => e.VisibleAt)];

    /// <summary>Wie oft die wartenden Mails schon empfangen wurden.</summary>
    public IReadOnlyList<long> Attempts => [.. main.Select(e => e.DequeueCount)];

    public void Clear()
    {
        main.Clear();
        dead.Clear();
        Failing = false;
        DeleteFails = false;
    }

    public Task EnsureCreated(CancellationToken ct) => Task.CompletedTask;

    public Task Enqueue(string body, CancellationToken ct)
    {
        if (Failing)
        {
            throw new InvalidOperationException("Queue nicht erreichbar.");
        }

        main.Add(new Entry(body, time.GetUtcNow()));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueuedMessage>> Receive(int max, TimeSpan visibility, CancellationToken ct) => Task.FromResult(Take(main, max, visibility));

    public Task Delete(QueuedMessage message, CancellationToken ct) =>
        DeleteFails ? throw new Azure.RequestFailedException(404, "Pop receipt mismatch", "PopReceiptMismatch", null) : Remove(main, message);

    public Task Postpone(QueuedMessage message, TimeSpan delay, CancellationToken ct)
    {
        Find(main, message).VisibleAt = time.GetUtcNow() + delay;
        return Task.CompletedTask;
    }

    public Task DeadLetter(string body, CancellationToken ct)
    {
        dead.Add(new Entry(body, time.GetUtcNow()));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<QueuedMessage>> ReceiveDeadLetters(int max, TimeSpan visibility, CancellationToken ct) => Task.FromResult(Take(dead, max, visibility));

    public Task DeleteDeadLetter(QueuedMessage message, CancellationToken ct) => Remove(dead, message);

    /// <summary>Wie Azure: Eine freigegebene Nachricht verliert ihren Platz in der Reihenfolge, sie kommt ans Ende.</summary>
    public Task ReleaseDeadLetter(QueuedMessage message, CancellationToken ct)
    {
        var entry = Find(dead, message);
        entry.VisibleAt = time.GetUtcNow();
        dead.Remove(entry);
        dead.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> PeekDeadLetters(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<string>>([.. dead.Where(e => e.VisibleAt <= time.GetUtcNow()).Take(32).Select(e => e.Body)]);

    public Task<MailQueueCounts> Counts(CancellationToken ct) => Task.FromResult(new MailQueueCounts(main.Count, dead.Count));

    private IReadOnlyList<QueuedMessage> Take(List<Entry> entries, int max, TimeSpan visibility)
    {
        var now = time.GetUtcNow();
        return [.. entries.Where(e => e.VisibleAt <= now).Take(max).Select(e =>
        {
            e.VisibleAt = now + visibility;
            e.DequeueCount++;
            e.PopReceipt = Guid.NewGuid().ToString();
            return new QueuedMessage(e.Id, e.PopReceipt, e.Body, e.DequeueCount);
        })];
    }

    private static Entry Find(List<Entry> entries, QueuedMessage message) =>
        entries.Single(e => e.Id == message.Id && e.PopReceipt == message.PopReceipt);

    private static Task Remove(List<Entry> entries, QueuedMessage message)
    {
        entries.Remove(Find(entries, message));
        return Task.CompletedTask;
    }

    private sealed class Entry(string body, DateTimeOffset visibleAt)
    {
        public string Id { get; } = Guid.NewGuid().ToString();

        public string Body { get; } = body;

        public DateTimeOffset VisibleAt { get; set; } = visibleAt;

        public long DequeueCount { get; set; }

        public string PopReceipt { get; set; } = "";
    }
}

/// <summary>Sender, der bei <see cref="Failing"/> eine Exception wirft, sonst wie <see cref="InMemoryMailSender"/>.</summary>
internal sealed class FlakyMailSender : IMailSender
{
    public ConcurrentQueue<MailMessage> Sent { get; } = new();

    public Exception? Failing { get; set; }

    public Task Send(MailMessage message, CancellationToken ct)
    {
        if (Failing is { } failing)
        {
            throw failing;
        }

        Sent.Enqueue(message);
        return Task.CompletedTask;
    }
}

/// <summary>Übersetzt ohne Claude API: hängt die Zielsprache an jede Zeile, das Markdown bleibt. <see cref="Translate"/> ersetzt das, etwa für kaputtes Markdown.</summary>
internal sealed class FakeTranslator : ITextTranslator
{
    public const int TokensPerCall = 1000;

    public int Calls { get; private set; }

    public Func<string, string, string>? Translate { get; set; }

    public string Version => "fake/1";

    Task<TranslatorReply> ITextTranslator.Translate(string sourceCulture, string targetCulture, IReadOnlyDictionary<string, string> fields, CancellationToken ct)
    {
        Calls++;
        var translate = Translate ?? ((text, target) => string.Join('\n', text.Split('\n').Select(line => line.Length == 0 ? line : $"{line} ({target})")));
        return Task.FromResult(new TranslatorReply(fields.ToDictionary(f => f.Key, f => translate(f.Value, targetCulture)), TokensPerCall, TokensPerCall));
    }

    public void Reset()
    {
        Calls = 0;
        Translate = null;
    }
}
