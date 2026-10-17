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
            options.RequestCultureProviders = [new PathRequestCultureProvider()];
        });

        return services;
    }

    /// <summary>`/` leitet nach `Accept-Language` auf eine Sprache weiter, sonst nach `/de` (`i18n.md`).</summary>
    public static IEndpointRouteBuilder MapLanguageRedirect(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/", (HttpRequest request) =>
            Results.Redirect($"/{Languages.FromAcceptLanguage(request.GetTypedHeaders().AcceptLanguage ?? [])}"));

        return endpoints;
    }
}
