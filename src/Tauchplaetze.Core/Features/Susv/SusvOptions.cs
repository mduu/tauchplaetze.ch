namespace Tauchplaetze.Core.Features.Susv;

/// <summary>Abruf der Karten auf susv.ch (Abschnitt `Susv`).</summary>
public class SusvOptions
{
    public const string Section = "Susv";

    /// <summary>Pause zwischen zwei Calls: höchstens 2 pro Sekunde (`data-sources.md`, «Höflich»). Die Tests setzen 0.</summary>
    public TimeSpan CallInterval { get; set; } = TimeSpan.FromMilliseconds(500);
}
