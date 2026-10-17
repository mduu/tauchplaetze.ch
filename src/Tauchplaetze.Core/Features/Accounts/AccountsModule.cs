using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts;

internal class AccountsModule : ICoreModule
{
    public void Register(IServiceCollection services) =>
        services.AddIdentityCore<User>(options => options.User.RequireUniqueEmail = true)
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TauchplaetzeDbContext>();
}
