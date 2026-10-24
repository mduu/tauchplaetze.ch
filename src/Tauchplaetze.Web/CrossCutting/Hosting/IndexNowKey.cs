using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.IndexNow;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>`/{key}.txt` mit dem Schlüssel von IndexNow (`seo.md`, «IndexNow»). Damit prüft die Suchmaschine, dass die Meldungen von uns kommen. Ohne Schlüssel 404.</summary>
internal static class IndexNowKey
{
    public static IEndpointRouteBuilder MapIndexNowKey(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/{key}.txt", (string key, IOptions<IndexNowOptions> options) =>
            options.Value.Key is { Length: > 0 } configured && string.Equals(key, configured, StringComparison.Ordinal)
                ? Results.Text(configured, "text/plain; charset=utf-8")
                : Results.NotFound());
        return endpoints;
    }
}
