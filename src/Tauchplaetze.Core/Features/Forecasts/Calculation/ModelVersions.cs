namespace Tauchplaetze.Core.Features.Forecasts.Calculation;

/// <summary>Vergleicht Regel-Versionen wie `rules-1.10` nach ihrer Nummer, nicht als Text (`rules-1.9` ist älter als `rules-1.10`).</summary>
internal static class ModelVersions
{
    public static string Newest(IEnumerable<string> versions) => versions.MaxBy(Number)!;

    private static Version Number(string version) =>
        Version.TryParse(version[(version.LastIndexOf('-') + 1)..], out var number) ? number : new Version(0, 0);
}
