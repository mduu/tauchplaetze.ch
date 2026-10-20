using System.Text.Json;
using Tauchplaetze.Core.CrossCutting.Storage;
using Tauchplaetze.Core.Features.Forecasts.Sources;

namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Status eines Calls im Lauf, als JSON in `ForecastRun.SourceStatus`.</summary>
internal record SourceCallStatus(string Source, string Key, bool Ok, int? StatusCode, string? Error, long DurationMs);

/// <summary>Legt jede Antwort unverändert in Blob Storage ab und merkt sich den Status jedes Calls eines Laufs.</summary>
internal class SourceRecorder(IRawDataStore rawData, string runPath)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly List<SourceCallStatus> calls = [];

    public bool AnyFailed => calls.Any(c => !c.Ok);

    public int Count => calls.Count;

    public string StatusJson => JsonSerializer.Serialize(calls, JsonOptions);

    public async Task<T?> Record<T>(string source, string key, Task<SourceResult<T>> call, CancellationToken ct, string extension = "json")
        where T : class
    {
        var result = await call;
        var response = result.Response;
        calls.Add(new SourceCallStatus(source, key, response.IsSuccess, response.StatusCode, response.Error, (long)response.Duration.TotalMilliseconds));

        if (response.Body is { } body)
        {
            await rawData.Save($"{runPath}/{source}/{key}.{extension}", body, ct);
        }

        return result.Value;
    }

    /// <summary>Legt eine Beilage zu den Rohdaten ab, etwa welche Punkte ein gebündelter Call abfragte. Zählt nicht als Call.</summary>
    public Task Attach(string source, string key, string body, CancellationToken ct) =>
        rawData.Save($"{runPath}/{source}/{key}.json", body, ct);
}
