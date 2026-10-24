using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace Tauchplaetze.Core.Features.IndexNow;

/// <summary>Meldet Adressen an den gemeinsamen Endpoint von IndexNow. Er gibt sie an Bing und die anderen Teilnehmer weiter.</summary>
internal class IndexNowClient(HttpClient http, IOptions<IndexNowOptions> options)
{
    public async Task<HttpStatusCode> Submit(Uri site, IReadOnlyList<string> urls, CancellationToken ct)
    {
        var key = options.Value.Key!;
        var body = new { host = site.Host, key, keyLocation = new Uri(site, $"/{key}.txt").ToString(), urlList = urls };
        using var response = await http.PostAsJsonAsync(options.Value.Endpoint, body, ct);
        return response.StatusCode;
    }
}
