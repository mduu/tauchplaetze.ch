using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Tauchplaetze.Core.Features.Forecasts.Sources;

namespace Tauchplaetze.Core.Features.Susv.Sources;

/// <summary>
/// Client für die Karten auf susv.ch (`stories/002-susv-import-und-moderation.md`, «Endpunkt»).
/// Die Schnittstelle ist nicht als öffentliche API beschrieben. Der SUSV erlaubt den Abruf (25.11.2026).
/// </summary>
internal class SusvClient(HttpClient httpClient)
{
    public const string Source = "susv";

    public Task<SourceResult<SusvMap>> GetMap(string culture, int filter, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { mapFilter = new[] { filter }, lang = culture });
        return Post($"{culture}/api/v1/map-result", body, json => SusvMap.Parse(json, httpClient.BaseAddress!), ct);
    }

    /// <summary>Holt die Seite eines Eintrags. Nur Links auf denselben Host wie die Schnittstelle.</summary>
    public async Task<SourceResult<string>> GetDetail(string url, string title, CancellationToken ct)
    {
        var uri = new Uri(url);
        if (!string.Equals(uri.Host, httpClient.BaseAddress!.Host, StringComparison.OrdinalIgnoreCase))
        {
            return new SourceResult<string>(new SourceResponse(url, null, null, "Fremder Host", TimeSpan.Zero), null);
        }

        var result = await SourceHttp.Get(httpClient, uri.PathAndQuery.TrimStart('/'), html => SusvDetailPage.MainText(html, title) ?? "", ct);
        return result.Value is { Length: > 0 } ? result : result with { Value = null };
    }

    private async Task<SourceResult<T>> Post<T>(string relativeUrl, string body, Func<string, T> parse, CancellationToken ct)
        where T : class
    {
        var url = new Uri(httpClient.BaseAddress!, relativeUrl).ToString();
        var stopwatch = Stopwatch.StartNew();
        SourceResponse response;
        try
        {
            using var content = new StringContent(body, Encoding.UTF8, "application/json");
            using var reply = await httpClient.PostAsync(relativeUrl, content, ct);
            var text = await reply.Content.ReadAsStringAsync(ct);
            response = reply.IsSuccessStatusCode
                ? new SourceResponse(url, (int)reply.StatusCode, text, null, stopwatch.Elapsed)
                : new SourceResponse(url, (int)reply.StatusCode, null, text.Length <= 300 ? text : text[..300] + "…", stopwatch.Elapsed);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or TaskCanceledException or TimeoutException
                                       or TimeoutRejectedException or BrokenCircuitException)
        {
            return new SourceResult<T>(new SourceResponse(url, null, null, ex.Message, stopwatch.Elapsed), null);
        }

        if (!response.IsSuccess)
        {
            return new SourceResult<T>(response, null);
        }

        try
        {
            return new SourceResult<T>(response, parse(response.Body!));
        }
        catch (Exception ex) when (ex is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return new SourceResult<T>(response with { Error = $"Parse: {ex.GetType().Name}: {ex.Message}" }, null);
        }
    }
}
