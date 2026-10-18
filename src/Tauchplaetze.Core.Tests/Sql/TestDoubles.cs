using System.Collections.Concurrent;
using System.Net;
using Tauchplaetze.Core.CrossCutting.Locks;
using Tauchplaetze.Core.CrossCutting.Mail;
using Tauchplaetze.Core.CrossCutting.Storage;
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
        ("models=meteoswiss_icon_ch1", "open-meteo", "forecast-meteoswiss_icon_ch1.json"),
        ("models=meteoswiss_icon_ch2", "open-meteo", "forecast-meteoswiss_icon_ch2.json"),
        ("models=icon_seamless", "open-meteo", "forecast-icon_seamless.json"),
        ("parameters=flow", "hydro", "daterange-2104-flow.json"),
    ];

    public ConcurrentBag<string> Requests { get; } = [];

    public string? Failing { get; set; }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);

        var route = Routes.FirstOrDefault(r => url.Contains(r.UrlPart, StringComparison.Ordinal));
        if (route.File is null || (Failing is { } failing && url.Contains(failing, StringComparison.Ordinal)))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound) { Content = new StringContent("not found") });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(TestData.Read(route.Source, route.File)) });
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

    /// <summary>Der Link in der letzten Mail an <paramref name="to"/>.</summary>
    public string LinkTo(string to) =>
        Sent.Last(m => m.To == to).Body.Split('\n').Single(line => line.StartsWith("https://", StringComparison.Ordinal)).Trim();
}
