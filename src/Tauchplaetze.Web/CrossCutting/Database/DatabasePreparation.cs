using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Core.Features.Shipping.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;

namespace Tauchplaetze.Web.CrossCutting.Database;

internal static class DatabasePreparation
{
    public const string Setting = "Database:PrepareOnStartup";

    public const string AdminsSection = "Accounts:Admins";

    /// <summary>
    /// Migriert die Datenbank, legt fehlende Gewässer und Admins an, importiert fehlende Plätze und ergänzt das Datum der ersten Beschreibung,
    /// wenn <see cref="Setting"/> gesetzt ist (lokal mit Aspire und in Prod). Ohne Admin in <see cref="AdminsSection"/> fehlt der Autor, und der Import entfällt.
    /// </summary>
    public static async Task PrepareDatabase(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(Setting))
        {
            return;
        }

        var started = TimeProvider.System.GetTimestamp();
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await Prepare(app);
                return;
            }
            catch (Exception ex) when (TimeProvider.System.GetElapsedTime(started) < Patience)
            {
                app.Logger.LogWarning(ex, "Database preparation failed (attempt {Attempt}), retrying in {Delay}", attempt, RetryDelay);
                await Task.Delay(RetryDelay);
            }
        }
    }

    /// <summary>
    /// Beim Deploy startet App Service die Managed Identity neu. Einige Minuten lang scheitert dann die Anmeldung bei der Datenbank
    /// (Deploy vom 27.11.2026: gut 4 Minuten). Die App wartet so lange, statt abzustürzen und einen zweiten Startzyklus auszulösen.
    /// Die Startfrist von App Service (`WEBSITES_CONTAINER_START_TIME_LIMIT`, 10 Minuten, `deploy/modules/webApp.bicep`) liegt darüber.
    /// </summary>
    private static readonly TimeSpan Patience = TimeSpan.FromMinutes(8);

    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(20);

    private static async Task Prepare(WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<ICommandHandler<MigrateDatabaseCommand, Unit>>().Handle(new MigrateDatabaseCommand());
        await services.GetRequiredService<ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>>().Handle(new SeedWaterBodiesCommand());
        await services.GetRequiredService<ICommandHandler<SeedShippingFeaturesCommand, SeedShippingFeaturesResult>>().Handle(new SeedShippingFeaturesCommand());

        var admins = app.Configuration.GetSection(AdminsSection).Get<List<AdminAccount>>() ?? [];
        var adminIds = await services.GetRequiredService<ICommandHandler<EnsureAdminsCommand, EnsureAdminsResult>>().Handle(new EnsureAdminsCommand(admins));

        if (adminIds.UserIds is [var editorId, ..])
        {
            await services.GetRequiredService<ICommandHandler<ImportDiveSitesCommand, ImportDiveSitesResult>>().Handle(new ImportDiveSitesCommand(editorId));
        }
        else
        {
            app.Logger.LogWarning("No admin in {Section}. The import of dive sites is skipped.", AdminsSection);
        }

        await services.GetRequiredService<ICommandHandler<BackfillFirstDescribedCommand, BackfillFirstDescribedResult>>().Handle(new BackfillFirstDescribedCommand());
    }
}
