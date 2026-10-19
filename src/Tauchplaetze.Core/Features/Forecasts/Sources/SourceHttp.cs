using System.Diagnostics;
using System.Globalization;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Tauchplaetze.Core.Features.Forecasts.Sources;

/// <summary>Ein Call an eine externe Quelle. Ein Fehler ist ein Ergebnis, keine Exception.</summary>
internal record SourceResponse(string Url, int? StatusCode, string? Body, string? Error, TimeSpan Duration)
{
    public bool IsSuccess => Body is not null && Error is null;
}

/// <summary>Ein Call mit dem gelesenen Wert. <see cref="Value"/> ist <c>null</c>, wenn der Call scheiterte.</summary>
internal record SourceResult<T>(SourceResponse Response, T? Value) where T : class;

internal static class SourceHttp
{
    public static async Task<SourceResponse> Get(HttpClient client, string relativeUrl, CancellationToken ct)
    {
        var url = new Uri(client.BaseAddress!, relativeUrl).ToString();
        var stopwatch = Stopwatch.StartNew();
        try
        {
            using var response = await client.GetAsync(relativeUrl, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return response.IsSuccessStatusCode
                ? new SourceResponse(url, (int)response.StatusCode, body, null, stopwatch.Elapsed)
                : new SourceResponse(url, (int)response.StatusCode, null, Truncate(body), stopwatch.Elapsed);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException
                                       or TimeoutRejectedException or BrokenCircuitException)
        {
            return new SourceResponse(url, null, null, ex.Message, stopwatch.Elapsed);
        }
    }

    /// <summary>Ein Call mit Parser. Scheitert der Parser, ist der Call gescheitert; der Body bleibt für die Rohdaten erhalten.</summary>
    public static async Task<SourceResult<T>> Get<T>(HttpClient client, string relativeUrl, Func<string, T> parse, CancellationToken ct)
        where T : class
    {
        var response = await Get(client, relativeUrl, ct);
        if (!response.IsSuccess)
        {
            return new SourceResult<T>(response, null);
        }

        try
        {
            return new SourceResult<T>(response, parse(response.Body!));
        }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
        {
            return new SourceResult<T>(response with { Error = $"Parse: {ex.GetType().Name}: {ex.Message}" }, null);
        }
    }

    public static string Format(double value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static string Truncate(string text) => text.Length <= 300 ? text : text[..300] + "…";
}
