using Tauchplaetze.Core;
using Tauchplaetze.Web.Components;
using Tauchplaetze.Web.CrossCutting.Database;
using Tauchplaetze.Web.CrossCutting.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTauchplaetzeCore();

builder.Services.AddPublicLocalization();
builder.Services.AddRazorComponents();

var app = builder.Build();

await app.PrepareDatabase();

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
