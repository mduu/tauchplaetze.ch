using Microsoft.AspNetCore.Localization;

namespace Tauchplaetze.Web.CrossCutting.Localization;

/// <summary>Liest die Sprache aus dem ersten Segment der URL (`/de/…`, `/en/…`).</summary>
internal class PathRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var language = FirstSegment(httpContext.Request.Path);

        if (!Languages.IsSupported(language))
        {
            return NullProviderCultureResult;
        }

        var culture = Languages.CultureOf(language!).Name;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }

    private static string? FirstSegment(PathString path) =>
        path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
}
