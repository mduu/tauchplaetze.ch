namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Art eines gescheiterten Calls an eine Quelle.</summary>
public enum FailureKind
{
    /// <summary>Die Quelle antwortete mit einem HTTP-Fehler (siehe Status-Code).</summary>
    Http,

    /// <summary>Kein Ergebnis in der erlaubten Zeit.</summary>
    Timeout,

    /// <summary>Der Circuit Breaker war offen, der Call ging gar nicht raus.</summary>
    CircuitOpen,

    /// <summary>Die Antwort liess sich nicht lesen.</summary>
    Parse,

    /// <summary>Keine Verbindung oder ein anderer Fehler im Netz.</summary>
    Network,
}

/// <summary>Gescheiterte Calls einer Quelle mit derselben Art: wie oft, welche Keys, welche Meldungen.</summary>
public record FailureGroup(string Source, FailureKind Kind, int? StatusCode, int Count, IReadOnlyList<string> Keys, IReadOnlyList<string> Messages);

/// <summary>Fasst die gescheiterten Calls eines Laufs zu Gruppen zusammen (`cms.md`, «Prognose-Status»). Die grössten Gruppen zuerst.</summary>
internal static class RunFailures
{
    public static IReadOnlyList<FailureGroup> Group(IEnumerable<SourceCallStatus> calls) =>
        [.. calls
            .Where(c => !c.Ok)
            .GroupBy(c => (c.Source, Kind: KindOf(c), StatusCode: KindOf(c) == FailureKind.Http ? c.StatusCode : null))
            .Select(g => new FailureGroup(
                g.Key.Source, g.Key.Kind, g.Key.StatusCode, g.Count(),
                [.. g.Select(c => c.Key).Distinct().Order(StringComparer.Ordinal)],
                [.. g.Select(c => c.Error ?? "").Where(e => e.Length > 0).Distinct()]))
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Source, StringComparer.Ordinal)];

    public static FailureKind KindOf(SourceCallStatus call) => call switch
    {
        { Error: { } error } when error.StartsWith("Parse:", StringComparison.Ordinal) => FailureKind.Parse,
        { StatusCode: not null } => FailureKind.Http,
        { Error: { } error } when error.Contains("circuit", StringComparison.OrdinalIgnoreCase) => FailureKind.CircuitOpen,
        { Error: { } error } when error.Contains("timeout", StringComparison.OrdinalIgnoreCase) || error.Contains("timed out", StringComparison.OrdinalIgnoreCase)
            || error.Contains("canceled", StringComparison.OrdinalIgnoreCase) => FailureKind.Timeout,
        _ => FailureKind.Network,
    };
}
