using Tauchplaetze.Core.CrossCutting.UseCases;
using Tauchplaetze.Core.Features.Operations.Commands;
using Tauchplaetze.Core.Features.WaterBodies.Commands;

namespace Tauchplaetze.Web.CrossCutting.Database;

internal static class DatabasePreparation
{
    public const string Setting = "Database:PrepareOnStartup";

    /// <summary>Migriert die Datenbank und legt fehlende Stammdaten an, wenn <see cref="Setting"/> gesetzt ist (lokal mit Aspire).</summary>
    public static async Task PrepareDatabase(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>(Setting))
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<MigrateDatabaseCommand, Unit>>().Handle(new MigrateDatabaseCommand());
        await scope.ServiceProvider.GetRequiredService<ICommandHandler<SeedWaterBodiesCommand, SeedWaterBodiesResult>>().Handle(new SeedWaterBodiesCommand());
    }
}
