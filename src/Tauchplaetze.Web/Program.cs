using MudBlazor.Services;
using Tauchplaetze.Core;
using Tauchplaetze.Web.Components;
using Tauchplaetze.Web.CrossCutting.Database;
using Tauchplaetze.Web.CrossCutting.Localization;
using Tauchplaetze.Web.Features.About;
using Tauchplaetze.Web.Features.Accounts;
using Tauchplaetze.Web.Features.Forecasts;
using Tauchplaetze.Web.Features.Mail;
using Tauchplaetze.Web.Features.Media;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTauchplaetzeCore();

builder.Services.AddPublicLocalization();
builder.Services.AddAccountCookie();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.Configure<ForecastSchedule>(builder.Configuration.GetSection(ForecastSchedule.Section));
builder.Services.AddHostedService<ForecastWorker>();
builder.Services.AddHostedService<MediaPurgeWorker>();
builder.Services.AddHostedService<MailQueueWorker>();
builder.Services.AddHostedService<DeadLetterMonitorWorker>();
builder.Services.Configure<OperatorOptions>(builder.Configuration.GetSection(OperatorOptions.Section));
builder.Services.AddContactRateLimit(builder.Configuration);

var app = builder.Build();

await app.PrepareDatabase();

if (app.Configuration.GetSection(OperatorOptions.Section).Get<OperatorOptions>() is not { IsComplete: true })
{
    app.Logger.LogWarning("Operator name or address in {Section} is missing. The legal notice shows a placeholder.", OperatorOptions.Section);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapLanguageRedirect();
app.MapAccountEndpoints();
app.MapMediaEndpoints();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapDefaultEndpoints();

app.Run();

/// <summary>Einstieg der Web-App. Öffentlich für die Integration Tests (`WebApplicationFactory`).</summary>
public partial class Program;
