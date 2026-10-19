using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Tauchplaetze.Core.CrossCutting.Modules;
using Tauchplaetze.Core.Data;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts;

internal class AccountsModule : ICoreModule
{
    /// <remarks>
    /// Passwort ab 10 Zeichen ohne Vorgaben zur Zusammensetzung (NIST SP 800-63B). Nach 5 Fehlversuchen 15 Minuten Lockout.
    /// Die Tokens für Bestätigung und neues Passwort gelten einen Tag (<see cref="ProtectedTokenProvider"/>).
    /// </remarks>
    public void Register(IServiceCollection services)
    {
        services.AddDataProtection();
        services.AddIdentityCore<User>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
                options.Password.RequiredLength = 10;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<TauchplaetzeDbContext>()
            .AddTokenProvider<ProtectedTokenProvider>(TokenOptions.DefaultProvider);
        services.AddScoped<AccountRoles>();
        services.AddScoped<AccountDeletion>();
    }
}
