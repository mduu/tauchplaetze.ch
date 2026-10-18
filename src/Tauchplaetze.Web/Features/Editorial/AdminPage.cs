using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Localization;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Web.Localization;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>
/// Basis der Seiten im CMS. Jeder Handler läuft in einem eigenen Scope: Eine Verbindung von Blazor Server lebt lange,
/// ein geteilter DbContext würde veraltete Daten halten.
/// </summary>
public abstract class AdminPage : ComponentBase
{
    [Inject]
    protected IServiceScopeFactory Scopes { get; set; } = default!;

    [Inject]
    protected IStringLocalizer<AdminStrings> L { get; set; } = default!;

    [CascadingParameter]
    protected Task<AuthenticationState> AuthenticationState { get; set; } = default!;

    protected ClaimsPrincipal User { get; private set; } = new();

    protected string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    protected bool IsModerator => User.IsInRole(Roles.Moderator) || User.IsInRole(Roles.Admin);

    protected bool IsAdmin => User.IsInRole(Roles.Admin);

    protected static string Language => System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;

    protected override async Task OnInitializedAsync()
    {
        User = (await AuthenticationState).User;
        await Load();
    }

    protected virtual Task Load() => Task.CompletedTask;

    protected async Task<TResult> Query<TQuery, TResult>(TQuery query)
    {
        await using var scope = Scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IQueryHandler<TQuery, TResult>>().Handle(query);
    }

    protected async Task<TResult> Command<TCommand, TResult>(TCommand command)
    {
        await using var scope = Scopes.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ICommandHandler<TCommand, TResult>>().Handle(command);
    }
}
