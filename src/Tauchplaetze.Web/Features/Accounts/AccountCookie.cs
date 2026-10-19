using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Features.Accounts;

/// <summary>Das Cookie der Anmeldung. Der Core prüft Passwort und Sperre, die Web-App schreibt nur das Cookie.</summary>
internal static class AccountCookie
{
    public const string CultureClaim = "culture";

    public static IServiceCollection AddAccountCookie(this IServiceCollection services)
    {
        services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "tauchplaetze.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.Redirect($"{AccountPaths.SignIn(LanguageOf(context.HttpContext))}?returnUrl={Uri.EscapeDataString(context.Request.Path + context.Request.QueryString)}");
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });
        services.AddAuthorization();
        services.AddCascadingAuthenticationState();
        return services;
    }

    public static Task SignIn(HttpContext context, SignedInAccount account)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, account.UserId),
            new(ClaimTypes.Name, account.DisplayName),
            new(CultureClaim, account.Culture),
            .. account.Roles.Select(role => new Claim(ClaimTypes.Role, role)),
        ];
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        return context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties { IsPersistent = true });
    }

    public static string? UserId(ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier);

    /// <summary>Nur lokale Pfade als Ziel nach der Anmeldung. Ein fremder Host wäre eine offene Weiterleitung.</summary>
    public static string SafeReturnUrl(string? returnUrl, string fallback) =>
        returnUrl is { Length: > 1 } && returnUrl[0] == '/' && returnUrl[1] != '/' && returnUrl[1] != '\\' ? returnUrl : fallback;

    private static string LanguageOf(HttpContext context)
    {
        var first = context.Request.Path.Value?.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return Languages.IsSupported(first) ? first! : Languages.Default;
    }
}

/// <summary>Pfade der Konto-Seiten. Das Segment `account` ist in allen Sprachen gleich: Die Seiten haben `noindex`.</summary>
internal static class AccountPaths
{
    public static string SignIn(string language) => $"/{language}/account/sign-in";

    public static string Register(string language) => $"/{language}/account/register";

    public static string Forgot(string language) => $"/{language}/account/forgot-password";

    public static string Profile(string language) => $"/{language}/account/profile";

    public static string MyData(string language) => $"/{language}/account/my-data";

    public static string Proposals(string language) => $"/{language}/account/proposals";

    public static string Delete(string language) => $"/{language}/account/delete";

    public static string SignOut(string language) => $"/{language}/account/sign-out";

    public static string ConfirmTemplate(string language) => $"/{language}/account/confirm?user={{user}}&token={{token}}";

    public static string ResetTemplate(string language) => $"/{language}/account/reset-password?user={{user}}&token={{token}}";
}
