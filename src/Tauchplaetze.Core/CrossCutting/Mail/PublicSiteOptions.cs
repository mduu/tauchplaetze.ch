namespace Tauchplaetze.Core.CrossCutting.Mail;

/// <summary>
/// Adresse der öffentlichen Website für Links in Mails (`PublicSite`). Sie kommt aus der Konfiguration,
/// nie aus der Anfrage: Ein gefälschter Host-Header würde sonst Links auf eine fremde Website erzeugen.
/// </summary>
public class PublicSiteOptions
{
    public const string Section = "PublicSite";

    public string BaseUri { get; set; } = "https://tauchplaetze.ch";

    /// <summary>Absoluter Link zu einem Pfad der Website. Der Pfad beginnt mit `/`.</summary>
    public string Link(string path) =>
        path.StartsWith('/') && !path.StartsWith("//", StringComparison.Ordinal)
            ? $"{BaseUri.TrimEnd('/')}{path}"
            : throw new ArgumentException($"Pfad «{path}» ist nicht relativ zur Website.", nameof(path));
}
