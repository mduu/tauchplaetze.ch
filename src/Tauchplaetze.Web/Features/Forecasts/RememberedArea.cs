namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>
/// Merkt sich Ort und Umkreis der Startseite in einem funktionalen Cookie (`ux.md`, «Startseite»; Datenschutz).
/// Ein Link mit <c>ort</c> hat Vorrang und ersetzt die Wahl. <see cref="AllOfSwitzerland"/> löscht sie.
/// Das Cookie enthält nur den Schlüssel des Orts und den Umkreis, nie Koordinaten.
/// </summary>
internal static class RememberedArea
{
    public const string CookieName = "tp_area";

    public const string AllOfSwitzerland = "schweiz";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    public static (Place? Place, int RadiusKm) Resolve(HttpContext? context, string? placeKey, int? radiusKm, TimeProvider time)
    {
        if (placeKey == AllOfSwitzerland)
        {
            context?.Response.Cookies.Delete(CookieName);
            return (null, Places.DefaultRadiusKm);
        }

        if (Places.Find(placeKey) is { } chosen)
        {
            var radius = Places.Radius(radiusKm);
            Remember(context, chosen, radius, time);
            return (chosen, radius);
        }

        return Read(context);
    }

    private static (Place? Place, int RadiusKm) Read(HttpContext? context)
    {
        var value = context?.Request.Cookies[CookieName];
        if (value?.Split(':') is [var key, var km] && Places.Find(key) is { } place)
        {
            return (place, Places.Radius(int.TryParse(km, out var parsed) ? parsed : null));
        }

        return (null, Places.DefaultRadiusKm);
    }

    private static void Remember(HttpContext? context, Place place, int radiusKm, TimeProvider time) =>
        context?.Response.Cookies.Append(CookieName, $"{place.Key}:{radiusKm}", new CookieOptions
        {
            Expires = time.GetUtcNow().Add(Lifetime),
            SameSite = SameSiteMode.Lax,
            Secure = true,
            HttpOnly = true,
            IsEssential = true,
            Path = "/",
        });
}
