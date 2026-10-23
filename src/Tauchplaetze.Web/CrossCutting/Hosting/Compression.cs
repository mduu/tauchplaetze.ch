using System.IO.Compression;
using Microsoft.AspNetCore.ResponseCompression;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>
/// Brotli und gzip für HTML und andere Antworten der App (`infrastructure.md`, «Kompression»). App Service komprimiert nicht selbst.
/// Auch über HTTPS: Die Antiforgery-Tokens in Formularen sind bei jeder Antwort anders verschlüsselt, BREACH findet so kein festes Geheimnis.
/// Statische Dateien liefert `MapStaticAssets` vorkomprimiert.
/// </summary>
internal static class Compression
{
    public static IServiceCollection AddCompression(this IServiceCollection services) =>
        services
            .Configure<BrotliCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest)
            .Configure<GzipCompressionProviderOptions>(options => options.Level = CompressionLevel.Fastest)
            .AddResponseCompression(options =>
            {
                options.EnableForHttps = true;
                options.Providers.Add<BrotliCompressionProvider>();
                options.Providers.Add<GzipCompressionProvider>();
            });
}
