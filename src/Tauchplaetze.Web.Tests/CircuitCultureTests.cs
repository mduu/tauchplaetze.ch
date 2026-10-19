using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Tauchplaetze.Web.CrossCutting.Localization;
using Tauchplaetze.Web.Features.Accounts;

namespace Tauchplaetze.Web.Tests;

public class CircuitCultureTests
{
    [Theory]
    [InlineData("/_blazor", "fr", "de", "fr-CH")]
    [InlineData("/_blazor/negotiate", "it", "de", "it-CH")]
    [InlineData("/_blazor", null, "en", null)]
    [InlineData("/admin", "fr", "de", null)]
    [InlineData("/_blazor", "xx", "de", null)]
    public async Task Die_Verbindung_von_Blazor_spricht_die_Sprache_der_Seite_die_sie_startete(string path, string? cookie, string profile, string? expected)
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = path;
        if (cookie is not null)
        {
            context.Request.Headers.Cookie = $"{CircuitCultureProvider.CookieName}={cookie}";
        }

        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(AccountCookie.CultureClaim, profile)], "test"));

        // Act
        var result = await new CircuitCultureProvider().DetermineProviderCultureResult(context);

        // Assert
        Assert.Equal(expected, result?.UICultures.Single().Value);
    }
}
