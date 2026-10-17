using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Features.Editorial.Commands;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;

namespace Tauchplaetze.Web.CrossCutting.Database;

internal static class DatabasePreparation
{
    public const string Setting = "Database:PrepareOnStartup";

    public const string AdminsSection = "Accounts:Admins";

    /// <summary>
    /// Migriert die Datenbank, legt fehlende Gewässer und Admins an und importiert fehlende Plätze,
    /// wenn <see cref="Setting"/> gesetzt ist (lokal mit Aspire). Ohne Admin in <see cref="AdminsSection"/> fehlt der Autor, und der Import entfällt.
    /// </summary>
    public static async Task PrepareDatabase(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(Setting))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        await services.GetRequiredService<ICommandHandler<MigrateDatabaseCommand, Unit>>().Handle(new MigrateDatabaseCommand());
        await services.GetRequiredService<ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>>().Handle(new SeedWaterBodiesCommand());

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
    }
}
