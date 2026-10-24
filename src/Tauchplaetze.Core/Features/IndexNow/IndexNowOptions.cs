using System.Text.RegularExpressions;

namespace Tauchplaetze.Core.Features.IndexNow;

/// <summary>Meldung geänderter Seiten an Suchmaschinen über IndexNow (`seo.md`, «IndexNow»). Lokal aus.</summary>
public partial class IndexNowOptions
{
    public const string Section = "IndexNow";

    public bool Enabled { get; set; }

    /// <summary>32–128 Zeichen aus Buchstaben, Ziffern und `-`. Er ist öffentlich: Die Website liefert ihn unter `/{Key}.txt` aus.</summary>
    public string? Key { get; set; }

    public string Endpoint { get; set; } = "https://api.indexnow.org/indexnow";

    /// <summary>Höchstens eine Meldung pro Intervall, gesammelt und ohne Doppel.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>So oft versucht der Job eine Meldung nach einem Fehler (429 oder 5xx) in den folgenden Intervallen erneut.</summary>
    public int MaxAttempts { get; set; } = 3;

    public bool IsActive => Enabled && !string.IsNullOrEmpty(Key);

    public bool HasValidKey => string.IsNullOrEmpty(Key) || KeyPattern().IsMatch(Key);

    [GeneratedRegex("^[a-zA-Z0-9-]{32,128}$")]
    private static partial Regex KeyPattern();
}
