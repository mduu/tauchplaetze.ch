using System.Diagnostics;

namespace Tauchplaetze.Spike.DataSources;

/// <summary>
/// The result of one call to an external source.
/// </summary>
/// <param name="Url">The requested URL.</param>
/// <param name="StatusCode">The HTTP status code, or <c>null</c> when no response arrived.</param>
/// <param name="Body">The response body, or <c>null</c> on failure.</param>
/// <param name="Error">The error text on failure.</param>
/// <param name="Duration">The duration of the call.</param>
public record SourceResponse(string Url, int? StatusCode, string? Body, string? Error, TimeSpan Duration)
{
    public bool IsSuccess => Body is not null && Error is null;
}

/// <summary>
/// Sends a GET request and keeps the raw body. A failure is a result, not an exception.
/// </summary>
internal static class SourceHttp
{
    public static async Task<SourceResponse> Get(
        HttpClient client, string relativeUrl, RawResponseStore store, string source, string key, CancellationToken ct)
    {
        var url = new Uri(client.BaseAddress!, relativeUrl).ToString();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(relativeUrl, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            await store.Save(source, key, body, ct);
            if (!response.IsSuccessStatusCode)
            {
                return new SourceResponse(url, (int)response.StatusCode, null, Truncate(body), stopwatch.Elapsed);
            }

            return new SourceResponse(url, (int)response.StatusCode, body, null, stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException
                                       or Polly.Timeout.TimeoutRejectedException
                                       or Polly.CircuitBreaker.BrokenCircuitException)
        {
            return new SourceResponse(url, null, null, ex.Message, stopwatch.Elapsed);
        }
    }

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
