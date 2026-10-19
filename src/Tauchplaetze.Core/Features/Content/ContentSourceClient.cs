using System.Net;
using Microsoft.Extensions.Logging;

namespace Tauchplaetze.Core.Features.Content;

/// <summary>Holt das Inhaltspaket von der Quelle (Prod) über den Endpoint mit Schlüssel (`Content:SourceBaseUri`, `Content:ExportKey`).</summary>
internal class ContentSourceClient(HttpClient httpClient, ILogger<ContentSourceClient> logger)
{
    /// <summary>Lädt das Paket in eine temporäre Datei, die beim Schliessen verschwindet. <c>null</c> bei einem Fehler der Quelle oder einem zu grossen Paket.</summary>
    public async Task<Stream?> Download(Uri baseUri, string key, long maxBytes, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(baseUri, ContentOptions.ExportPath));
        request.Headers.Add(ContentOptions.KeyHeader, key);
        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("Content pull from {Host} failed with status {Status}", baseUri.Host, (int)response.StatusCode);
            return null;
        }

        if (response.Content.Headers.ContentLength > maxBytes)
        {
            logger.LogWarning("Content pull from {Host} refused: package has {Bytes} bytes", baseUri.Host, response.Content.Headers.ContentLength);
            return null;
        }

        await using var body = await response.Content.ReadAsStreamAsync(ct);
        return await ContentRules.Seekable(body, maxBytes, ct);
    }

    /// <summary>Ohne Weiterleitung: Der Schlüssel geht nur an die konfigurierte Adresse.</summary>
    public static HttpMessageHandler Handler() => new SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.None };
}
