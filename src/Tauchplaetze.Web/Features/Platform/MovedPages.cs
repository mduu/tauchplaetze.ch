using System.Globalization;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Features.Platform;

/// <summary>
/// Alte Adressen aus dem CMS leiten auf ihren neuen Ort (`cms.md`, «Bereiche»). Mails und Lesezeichen zeigen noch dorthin.
/// «Meine Vorschläge» mit 302: Das Ziel hängt von der Sprache im Profil ab.
/// </summary>
internal static class MovedPages
{
    public static IEndpointRouteBuilder MapMovedPages(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/admin/prognose", () => Results.Redirect("/platform/prognose", permanent: true));
        endpoints.MapGet("/admin/meine-vorschlaege", () => Results.Redirect(AccountPaths.Proposals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName)));
        return endpoints;
    }
}
