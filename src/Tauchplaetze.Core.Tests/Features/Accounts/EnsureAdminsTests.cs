using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.Data.Entities;
using Tauchplaetze.Core.Features.Accounts;
using Tauchplaetze.Core.Features.Accounts.Commands;
using Tauchplaetze.Core.Tests.Sql;

namespace Tauchplaetze.Core.Tests.Features.Accounts;

public class EnsureAdminsTests(SqlServer sql) : SqlTest(sql)
{
    [SqlFact]
    public async Task Ein_Admin_aus_der_Konfiguration_wird_einmal_angelegt_und_hat_die_Rolle_Admin()
    {
        // Arrange
        var command = new EnsureAdminsCommand([new AdminAccount("admin@example.org", "Admin")]);

        // Act
        var first = await Command<EnsureAdminsCommand, EnsureAdminsResult>(command);
        var second = await Command<EnsureAdminsCommand, EnsureAdminsResult>(command);

        // Assert
        Assert.Equal(first.UserIds, second.UserIds);
        await using var scope = Sql.Services.CreateAsyncScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<User>>();
        var admin = await users.FindByIdAsync(first.UserIds.Single());
        Assert.True(await users.IsInRoleAsync(admin!, Roles.Admin));
    }
}
