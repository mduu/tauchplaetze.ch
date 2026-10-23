using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Extensions.Options;
using Tauchplaetze.Web.Features.Forecasts;

namespace Tauchplaetze.Web.CrossCutting.Caching;

/// <summary>Einstellungen des Output Cache der öffentlichen Seiten (`OutputCache`, `architecture.md`, «Caching»).</summary>
internal class PublicPageCacheOptions
{
    public const string Section = "OutputCache";

    /// <summary>Wie lange eine Seite höchstens gilt. 0 schaltet den Cache aus.</summary>
    public int Minutes { get; set; } = 60;
}

/// <summary>
/// Output Cache der öffentlichen Seiten für Besucher ohne Anmeldung. Eine Seite gilt höchstens <see cref="PublicPageCacheOptions.Minutes"/>
/// und nie über Mitternacht (Schweizer Zeit) hinaus, weil «heute» und die Linie «jetzt» sonst falsch wären.
/// Commands leeren den Cache (<see cref="CommandCacheEviction"/>). Nur für Seiten ohne Formular mit POST.
/// </summary>
internal static class PublicPageCache
{
    public const string Policy = "public-page";

    /// <summary>Alle Seiten im Cache.</summary>
    public const string AllPages = "public";

    /// <summary>Seiten mit Messwerten des Abfluss-Jobs.</summary>
    public const string DischargePages = "discharge";

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    public static IServiceCollection AddPublicPageCache(this IServiceCollection services, IConfiguration configuration) =>
        services.Configure<PublicPageCacheOptions>(configuration.GetSection(PublicPageCacheOptions.Section))
            .AddOutputCache(options => options.AddPolicy(Policy, policy => policy
                .AddPolicy<ExpireBeforeMidnight>()
                .AddPolicy<WithoutAntiforgeryCookie>()
                .Tag(AllPages)
                .VaryByValue(context => new KeyValuePair<string, string>("area", context.Request.Cookies[RememberedArea.CookieName] ?? ""))
                .VaryByValue(context => new KeyValuePair<string, string>("enhanced", IsEnhancedNavigation(context.Request) ? "1" : "0"))));

    /// <summary>Markiert die Seite der laufenden Anfrage, damit ein Command sie gezielt aus dem Cache nimmt.</summary>
    public static void Tag(HttpContext? context, string tag) =>
        context?.Features.Get<IOutputCacheFeature>()?.Context.Tags.Add(tag);

    /// <summary>Die Dauer bis zum Ablauf: die eingestellte Dauer, höchstens bis Mitternacht.</summary>
    public static TimeSpan Lifetime(DateTimeOffset now, TimeSpan maximum)
    {
        var local = TimeZoneInfo.ConvertTime(now, Zone);
        var midnight = new DateTimeOffset(local.Date.AddDays(1), Zone.GetUtcOffset(local.Date.AddDays(1)));
        var untilMidnight = midnight - local;
        return untilMidnight < maximum ? untilMidnight : maximum;
    }

    private static bool IsEnhancedNavigation(HttpRequest request) =>
        request.Headers.Accept.Any(value => value?.Contains("blazor-enhanced-nav=on", StringComparison.Ordinal) == true);

    private sealed class ExpireBeforeMidnight(IOptions<PublicPageCacheOptions> options, TimeProvider time) : IOutputCachePolicy
    {
        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            if (options.Value.Minutes <= 0)
            {
                context.EnableOutputCaching = false;
                return ValueTask.CompletedTask;
            }

            context.ResponseExpirationTimeSpan = Lifetime(time.GetUtcNow(), TimeSpan.FromMinutes(options.Value.Minutes));
            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Blazor setzt bei jeder Seite ohne Cookie das Antiforgery-Cookie. Eine Antwort mit Cookie speichert der Output Cache nie,
    /// Bots ohne Cookies füllten ihn so nie. Die Seiten im Cache haben kein Formular mit POST und brauchen das Cookie nicht.
    /// Andere Cookies (z.B. <see cref="RememberedArea.CookieName"/>) verhindern das Speichern weiterhin.
    /// </summary>
    private sealed class WithoutAntiforgeryCookie(IOptions<AntiforgeryOptions> antiforgery) : IOutputCachePolicy
    {
        public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
        {
            if (context.AllowCacheStorage)
            {
                var response = context.HttpContext.Response;
                response.OnStarting(() =>
                {
                    RemoveCookie(response, $"{antiforgery.Value.Cookie.Name}=");
                    return Task.CompletedTask;
                });
            }

            return ValueTask.CompletedTask;
        }

        public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

        public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation) => ValueTask.CompletedTask;

        private static void RemoveCookie(HttpResponse response, string prefix) =>
            response.Headers.SetCookie = response.Headers.SetCookie.Where(cookie => cookie?.StartsWith(prefix, StringComparison.Ordinal) != true).ToArray();
    }
}
