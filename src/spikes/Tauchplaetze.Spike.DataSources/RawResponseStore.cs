using Microsoft.Extensions.Options;

namespace Tauchplaetze.Spike.DataSources;

/// <summary>
/// Keeps each raw response as a file. The files become test data for the contract tests later.
/// </summary>
internal class RawResponseStore(IOptions<SpikeOptions> options, IHostEnvironment environment, TimeProvider time)
{
    private readonly string runDirectory = Path.Combine(
        Path.GetFullPath(options.Value.OutputDirectory, environment.ContentRootPath),
        time.GetUtcNow().ToString("yyyyMMdd-HHmmss"));

    /// <summary>
    /// The directory of the current run.
    /// </summary>
    public string RunDirectory => runDirectory;

    public Task Save(string source, string key, string content, CancellationToken ct) =>
        Write(Path.Combine(runDirectory, source), key, content, ct);

    /// <summary>
    /// Saves the derived report next to the raw responses.
    /// </summary>
    public Task SaveReport(string content, CancellationToken ct) => Write(runDirectory, "report", content, ct);

    private static async Task Write(string directory, string key, string content, CancellationToken ct)
    {
        Directory.CreateDirectory(directory);
        var fileName = string.Concat(key.Select(c => Path.GetInvalidFileNameChars().Contains(c) || c == '/' ? '_' : c));
        await File.WriteAllTextAsync(Path.Combine(directory, fileName + ".json"), content, ct);
    }
}
