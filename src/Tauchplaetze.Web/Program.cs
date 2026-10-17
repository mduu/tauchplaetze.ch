using Tauchplaetze.Core;
using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Web.Components;
using Tauchplaetze.Web.CrossCutting.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTauchplaetzeCore();

builder.Services.AddPublicLocalization();
builder.Services.AddRazorComponents();

var app = builder.Build();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<ICommandHandler<MigrateDatabaseCommand, Unit>>().Handle(new MigrateDatabaseCommand());
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapLanguageRedirect();
app.MapRazorComponents<App>();
app.MapDefaultEndpoints();

app.Run();

/// <summary>Einstieg der Web-App. Öffentlich für die Integration Tests (`WebApplicationFactory`).</summary>
public partial class Program;
