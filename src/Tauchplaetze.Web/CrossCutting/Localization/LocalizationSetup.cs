using Microsoft.AspNetCore.Localization;

namespace Tauchplaetze.Web.CrossCutting.Localization;

internal static class LocalizationSetup
{
    public static IServiceCollection AddPublicLocalization(this IServiceCollection services)
    {
        services.AddLocalization();
        services.Configure<RequestLocalizationOptions>(options =>
        {
            options.DefaultRequestCulture = new RequestCulture(Languages.CultureOf(Languages.Default));
            options.SupportedCultures = [.. Languages.Cultures];
            options.SupportedUICultures = [.. Languages.Cultures];
            options.RequestCultureProviders = [new PathRequestCultureProvider(), new ClaimRequestCultureProvider()];
        });

        return services;
    }

    /// <summary>
    /// `/` leitet nach `Accept-Language` auf eine Sprache weiter, sonst nach `/de` (`i18n.md`).
    /// `/{lang}/site/{slug}` führt auf den Platz mit dem Pfad der Sprache (Links in Mails und im CMS).
    /// </summary>
    public static IEndpointRouteBuilder MapLanguageRedirect(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (HttpRequest request) =>
            Results.Redirect($"/{Languages.FromAcceptLanguage(request.GetTypedHeaders().AcceptLanguage ?? [])}"));

        endpoints.MapGet("/{lang}/site/{slug}", (string lang, string slug) =>
            Languages.IsSupported(lang) ? Results.Redirect(PublicRoutes.DiveSite(lang, slug), permanent: true) : Results.NotFound());

        return endpoints;
    }
}
