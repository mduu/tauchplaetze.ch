using Microsoft.AspNetCore.Localization;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.CrossCutting.Localization;

/// <summary>Sprache der angemeldeten Person (`PreferredCulture`) für Pfade ohne Sprachpräfix: `/admin` und die Verbindung von Blazor (`i18n.md`).</summary>
internal class ClaimRequestCultureProvider : RequestCultureProvider
{
    public override Task<ProviderCultureResult?> DetermineProviderCultureResult(HttpContext httpContext)
    {
        var language = httpContext.User.FindFirst(AccountCookie.CultureClaim)?.Value;
        if (!Languages.IsSupported(language))
        {
            return NullProviderCultureResult;
        }

        var culture = Languages.CultureOf(language!).Name;
        return Task.FromResult<ProviderCultureResult?>(new ProviderCultureResult(culture, culture));
    }
}
