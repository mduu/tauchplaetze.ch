using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Tauchplaetze.Web.Tests;

/// <summary>Die Web-App im Speicher. Die Datenbank zeigt auf einen toten Port: Die öffentlichen Seiten dieser Tests lesen keine Daten.</summary>
public sealed class WebApp : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:tauchplaetze", "Server=127.0.0.1,1;Database=tauchplaetze;User Id=test;Password=test;TrustServerCertificate=true");

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });
}

internal static partial class VisibleText
{
    public static string Of(string html)
    {
        var body = BodyPattern().Match(html) is { Success: true } match ? match.Groups[1].Value : html;
        var withoutScripts = ScriptPattern().Replace(body, " ");
        var text = TagPattern().Replace(withoutScripts, " ");
        return WhitespacePattern().Replace(WebUtility.HtmlDecode(text), " ").Trim();
    }

    [GeneratedRegex("<body[^>]*>(.*)</body>", RegexOptions.Singleline)]
    private static partial Regex BodyPattern();

    [GeneratedRegex("<script.*?</script>", RegexOptions.Singleline)]
    private static partial Regex ScriptPattern();

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagPattern();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespacePattern();
}
