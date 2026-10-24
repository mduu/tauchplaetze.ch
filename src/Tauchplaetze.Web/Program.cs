using MudBlazor.Services;
using Tauchplaetze.Core;
using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Web.Components;
using Tauchplaetze.Web.CrossCutting.Caching;
using Tauchplaetze.Web.CrossCutting.Database;
using Tauchplaetze.Web.CrossCutting.Health;
using Tauchplaetze.Web.CrossCutting.Hosting;
using Tauchplaetze.Web.CrossCutting.Localization;
using Tauchplaetze.Web.CrossCutting.RateLimiting;
using Tauchplaetze.Web.CrossCutting.Security;
using Tauchplaetze.Web.Features.About;
using Tauchplaetze.Web.Features.Accounts;
using Tauchplaetze.Web.Features.Forecasts;
using Tauchplaetze.Web.Features.IndexNow;
using Tauchplaetze.Web.Features.Mail;
using Tauchplaetze.Web.Features.Media;
using Tauchplaetze.Web.Features.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddTauchplaetzeCore();
builder.Services.EvictPublicPagesAfterCommands();

builder.Services.AddPublicLocalization();
builder.Services.AddAccountCookie();
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddMudServices();
builder.Services.AddMemoryCache();
builder.Services.Configure<ForecastSchedule>(builder.Configuration.GetSection(ForecastSchedule.Section));
builder.Services.AddHostedService<ForecastWorker>();
builder.Services.AddHostedService<DischargeWorker>();
builder.Services.AddHostedService<MediaPurgeWorker>();
builder.Services.AddHostedService<UnconfirmedAccountsWorker>();
builder.Services.AddHostedService<MailQueueWorker>();
builder.Services.AddHostedService<DeadLetterMonitorWorker>();
builder.Services.AddHostedService<IndexNowWorker>();
builder.Services.Configure<OperatorOptions>(builder.Configuration.GetSection(OperatorOptions.Section));
builder.Services.AddRateLimits(builder.Configuration);
builder.Services.AddPublicPageCache(builder.Configuration);
builder.Services.AddSecurityHeaders();
builder.Services.AddCompression();
builder.Services.AddSingleton<FormBotTrap>();
builder.Services.AddHostRedirects(builder.Configuration);
ReadinessCheck.Register(builder.Services);

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

app.UseHostRedirects();
app.UseHeadAsGet();
app.UseRouting();
app.UseSecurityHeaders();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseRequestLocalization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAuthorization();
app.UseOutputCache();
app.UseRateLimiter();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapLanguageRedirect();
app.MapRobotsTxt();
app.MapSitemap();
app.MapIndexNowKey();
app.MapLlmsTxt();
app.MapAccountEndpoints();
app.MapMediaEndpoints();
app.MapContentEndpoints();
app.MapMovedPages();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode(options => options.ContentSecurityFrameAncestorsPolicy = null);
app.MapDefaultEndpoints();
ReadinessCheck.Map(app);

app.Run();

/// <summary>Einstieg der Web-App. Öffentlich für die Integration Tests (`WebApplicationFactory`).</summary>
public partial class Program;
