using Microsoft.AspNetCore.Localization;

namespace Tauchplaetze.Web.CrossCutting.Localization;

/// <summary>
/// Sprache der Blazor-Verbindung (`/_blazor`): Der Request trägt kein Sprachpräfix. Die interaktive Seite merkt sich beim Laden ihre
/// Sprache in einem technischen Cookie, die Verbindung übernimmt sie (`i18n.md`, «Interaktive Seiten»). Ohne Cookie gilt das Profil.
/// </summary>
internal class CircuitCultureProvider : RequestCultureProvider
{
    public const string CookieName = "tauchplaetze.lang";

    public const string CircuitPath = "/_blazor";

    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        if (!httpContext.Request.Path.StartsWithSegments(CircuitPath)
            || httpContext.Request.Cookies[CookieName] is not { } language
            || !Languages.IsSupported(language))
        {
            return NullProviderCultureResult;
        }

        var culture = Languages.CultureOf(language).Name;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }

    /// <summary>Merkt sich die Sprache der Seite für die Verbindung, die sie gleich startet.</summary>
    public static void Remember(HttpContext context, string language)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Cookies.Append(CookieName, language, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
        });
    }
}
