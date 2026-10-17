using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Features.Accounts;

namespace Tauchplaetze.Core.Tests.Sql;

public class DatabaseTests(SqlServer sql) : SqlTest(sql)
{
    [Fact]
    public void Das_Modell_entspricht_der_letzten_Migration()
    {
        // Arrange
        using var dbContext = new DesignTimeDbContextFactory().CreateDbContext([]);

        // Act
        var pending = dbContext.Database.HasPendingModelChanges();

        // Assert
        Assert.False(pending, "Das Modell hat Änderungen ohne Migration. «dotnet ef migrations add …» ausführen.");
    }

    [SqlFact]
    public async Task Die_Migrationen_laufen_auf_einer_leeren_Datenbank_und_legen_die_Rollen_an()
    {
        // Arrange
        await using var scope = Sql.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<TauchplaetzeDbContext>();

        // Act
        var pending = await dbContext.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken);
        var roles = await dbContext.Set<IdentityRole>().Select(r => r.Name).ToListAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(pending);
        Assert.Equivalent(Roles.All, roles);
    }
}
