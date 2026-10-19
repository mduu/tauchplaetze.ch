namespace Tauchplaetze.Core.Features.Content;

/// <summary>Holt das Inhaltspaket von der Quelle (Prod) über den Endpoint mit Schlüssel (`Content:SourceBaseUri`, `Content:ExportKey`).</summary>
internal class ContentSourceClient(HttpClient httpClient)
{
    public const string ExportPath = "api/platform/content-export";

    public const string KeyHeader = "X-Content-Export-Key";

    /// <summary>Lädt das Paket in eine temporäre Datei, die beim Schliessen verschwindet. <c>null</c> bei einem Fehler der Quelle oder einem zu grossen Paket.</summary>
    public async Task<Stream?> Download(Uri baseUri, string key, long maxBytes, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, ExportPath));
        request.Headers.Add(KeyHeader, key);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode || response.Content.Headers.ContentLength > maxBytes)
        {
            return null;
        }

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await ContentRules.Seekable(body, maxBytes, ct);
    }
}
