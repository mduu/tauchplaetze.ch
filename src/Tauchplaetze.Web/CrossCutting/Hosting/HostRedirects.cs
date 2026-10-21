using Microsoft.Extensions.Options;
using Tauchplaetze.Core.CrossCutting.Mail;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>
/// Weiterleitungen nach Host (`HostRedirects`, `seo.md`, «Domains»): Aliase wie `www.tauchplaetze.ch` auf den Host aus
/// `PublicSite:BaseUri`, Kurz-Domains wie `sichtweiten.ch` auf die Prognose oder eine Gewässerseite.
/// </summary>
public class HostRedirectOptions
{
    public const string Section = "HostRedirects";

    /// <summary>Hosts, die mit Pfad und Query auf den kanonischen Host gehen.</summary>
    public List<string> Aliases { get; set; } = [];

    /// <summary>Hosts wie `sichtweiten.ch`: Kurzpfad auf die Gewässerseite, sonst auf <see cref="ShortcutDefaultPath"/>.</summary>
    public List<string> ShortcutHosts { get; set; } = [];

    public string ShortcutDefaultPath { get; set; } = "/de";

    /// <summary>Kurzpfad (ohne `/`) → Slug des Gewässers, z.B. `zuerichsee` → `zuerichsee`.</summary>
    public Dictionary<string, string> Shortcuts { get; set; } = [];
}

/// <summary>Bestimmt das Ziel einer Weiterleitung nach Host. Das Ziel liegt immer auf dem konfigurierten kanonischen Host.</summary>
public class HostRedirects(HostRedirectOptions options, string canonicalBaseUri)
{
    private readonly Uri canonical = new(canonicalBaseUri.TrimEnd('/') + "/");

    /// <summary>Absolute Ziel-URL oder null, wenn der Host nicht umgeleitet wird.</summary>
    public string? Resolve(string host, string path, string query)
    {
        if (Matches(options.Aliases, host))
        {
            return Target(path, query);
        }

        if (Matches(options.ShortcutHosts, host))
        {
            var shortcut = path.Trim('/');
            return options.Shortcuts.FirstOrDefault(s => string.Equals(s.Key, shortcut, StringComparison.OrdinalIgnoreCase)) is { Value: { } slug }
                ? Target($"/de/gewaesser/{Uri.EscapeDataString(slug)}", "")
                : Target(options.ShortcutDefaultPath, "");
        }

        return null;
    }

    private static bool Matches(List<string> hosts, string host) =>
        hosts.Any(h => string.Equals(h, host, StringComparison.OrdinalIgnoreCase));

    private string Target(string path, string query) =>
        $"{canonical.Scheme}://{canonical.Authority}{(path.StartsWith('/') ? path : "/" + path)}{query}";
}

internal static class HostRedirectMiddleware
{
    public static IServiceCollection AddHostRedirects(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<HostRedirectOptions>(configuration.GetSection(HostRedirectOptions.Section))
            .AddSingleton(provider => new HostRedirects(
                provider.GetRequiredService<IOptions<HostRedirectOptions>>().Value,
                provider.GetRequiredService<IOptions<PublicSiteOptions>>().Value.BaseUri));

    /// <summary>
    /// GET und HEAD bekommen 301, andere Methoden 308: So bleibt die Methode erhalten, und kein Formular landet auf einem Alias-Host.
    /// Andere Hosts (z.B. `*.azurewebsites.net` für Health Check und Deploy) laufen unverändert weiter.
    /// </summary>
    public static IApplicationBuilder UseHostRedirects(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            var request = context.Request;
            var target = context.RequestServices.GetRequiredService<HostRedirects>()
                .Resolve(request.Host.Host, request.Path.Value ?? "/", request.QueryString.Value ?? "");
            if (target is null)
            {
                return next(context);
            }

            context.Response.StatusCode = HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)
                ? StatusCodes.Status301MovedPermanently
                : StatusCodes.Status308PermanentRedirect;
            context.Response.Headers.Location = target;
            return Task.CompletedTask;
        });
}
