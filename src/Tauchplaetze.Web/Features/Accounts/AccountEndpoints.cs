using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts.Queries;
using Tauchplaetze.Web.CrossCutting.Localization;

namespace Tauchplaetze.Web.Features.Accounts;

internal static class AccountEndpoints
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static IEndpointRouteBuilder MapAccountEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/{lang}/account/sign-out", async (string lang, HttpContext context) =>
        {
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Redirect($"/{(Languages.IsSupported(lang) ? lang : Languages.Default)}");
        });

        endpoints.MapGet("/{lang}/account/my-data.json", async (HttpContext context, IQueryHandler<GetPersonalDataQuery, PersonalData?> personalData) =>
        {
            var data = await personalData.Handle(new GetPersonalDataQuery(AccountCookie.UserId(context.User)!), context.RequestAborted);
            return data is null
                ? Results.NotFound()
                : Results.File(JsonSerializer.SerializeToUtf8Bytes(data, JsonOptions), "application/json", "tauchplaetze-meine-daten.json");
        }).RequireAuthorization();

        return endpoints;
    }
}
