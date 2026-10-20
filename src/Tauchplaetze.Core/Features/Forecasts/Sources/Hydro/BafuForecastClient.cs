using System.Globalization;
using System.Text.RegularExpressions;

namespace Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

/// <summary>Client für die Abflussvorhersagen des BAFU per HTTPS mit Basic Auth. Eine Datei pro Station und Variante.</summary>
internal class BafuForecastClient(HttpClient httpClient)
{
    public const string Source = "bafu";

    /// <summary>Ordner und Varianten in der Reihenfolge, in der der Import sie versucht: kontrolliert vor unkontrolliert, 5 Tage vor 2 Tagen.</summary>
    public static readonly IReadOnlyList<(string Folder, string Variant)> Candidates =
    [
        ("official", "ICH2_Ctrl"),
        ("official", "ICH1_Ctrl"),
        ("unofficial", "ICH2_Ctrl"),
        ("unofficial", "ICH1_Ctrl"),
    ];

    public Task<SourceResult<BafuForecastFile>> Get(string folder, string variant, string stationId, CancellationToken ct) =>
        SourceHttp.Get(
            httpClient,
            $"pqprevi-{folder}/Pqprevi_{variant}_{Uri.EscapeDataString(stationId)}.txt",
            BafuForecastFile.Parse,
            ct);
}

/// <summary>
/// Eine Vorhersagedatei des BAFU. <see cref="Hours"/>: Stundenmittel nach dem letzten Messwert, Beginn der Stunde in UTC.
/// Die Datei rechnet in MEWZ (immer UTC+1). Die Zeile `hh` ist das Mittel der Stunde, die um `hh` endet.
/// </summary>
internal partial record BafuForecastFile(DateTimeOffset IssuedAt, DateTimeOffset MeasuredUntil, IReadOnlyList<BafuForecastHour> Hours)
{
    private static readonly TimeSpan Mewz = TimeSpan.FromHours(1);

    private static readonly string[] DischargeColumns = ["Abfluss", "Débits", "Debits", "Q_ctl"];

    public static BafuForecastFile Parse(string text)
    {
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var issuedAt = HeaderTime(lines, "Ausgegeben am");
        var measuredUntil = HeaderTime(lines, "Gemessene Werte bis");
        var header = lines.FindIndex(l => l.StartsWith("dd mm yyyy hh", StringComparison.Ordinal));
        if (header < 0)
        {
            throw new FormatException("Kopfzeile «dd mm yyyy hh» fehlt.");
        }

        var columns = Tokens(lines[header]);
        var column = Array.FindIndex(columns, c => DischargeColumns.Contains(c, StringComparer.Ordinal));
        if (column < 0)
        {
            throw new FormatException("Spalte für den Abfluss fehlt.");
        }

        var hours = lines.Skip(header + 1)
            .Select(Tokens)
            .Where(t => t.Length > column)
            .Select(t => Hour(t, column))
            .OfType<BafuForecastHour>()
            .Where(h => h.Hour >= measuredUntil)
            .OrderBy(h => h.Hour)
            .ToList();
        return new BafuForecastFile(issuedAt, measuredUntil, hours);
    }

    private static BafuForecastHour? Hour(string[] tokens, int column)
    {
        if (!int.TryParse(tokens[0], out var day) || !int.TryParse(tokens[1], out var month) || !int.TryParse(tokens[2], out var year)
            || !int.TryParse(tokens[3], out var hour)
            || !decimal.TryParse(tokens[column], NumberStyles.Float, CultureInfo.InvariantCulture, out var discharge) || discharge < 0)
        {
            return null;
        }

        var end = new DateTimeOffset(year, month, day, 0, 0, 0, Mewz).AddHours(hour);
        return new BafuForecastHour(end.AddHours(-1).ToUniversalTime(), discharge);
    }

    private static DateTimeOffset HeaderTime(List<string> lines, string label)
    {
        var line = lines.FirstOrDefault(l => l.StartsWith(label, StringComparison.Ordinal))
            ?? throw new FormatException($"Kopfzeile «{label}» fehlt.");
        var match = HeaderTimePattern().Match(line);
        if (!match.Success)
        {
            throw new FormatException($"Zeit in «{label}» fehlt.");
        }

        int Part(int i) => int.Parse(match.Groups[i].Value, CultureInfo.InvariantCulture);
        return new DateTimeOffset(Part(3), Part(2), Part(1), 0, 0, 0, Mewz).AddHours(Part(4)).AddMinutes(Part(5)).ToUniversalTime();
    }

    private static string[] Tokens(string line) => line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    [GeneratedRegex(@"(\d{2})\.(\d{2})\.(\d{4}), (\d{2})\.(\d{2})")]
    private static partial Regex HeaderTimePattern();
}

/// <summary>Vorhergesagtes Stundenmittel des Abflusses in m³/s, Beginn der Stunde in UTC.</summary>
internal record BafuForecastHour(DateTimeOffset Hour, decimal Discharge);
