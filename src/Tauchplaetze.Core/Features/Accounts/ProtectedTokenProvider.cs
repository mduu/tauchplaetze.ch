using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>
/// Tokens für Bestätigung der E-Mail und neues Passwort. Sie gelten einen Tag und werden mit einem neuen Security Stamp ungültig.
/// </summary>
/// <remarks>Wie `DataProtectorTokenProvider` von ASP.NET Core Identity. Der Core referenziert ASP.NET Core nicht, darum die eigene Fassung.</remarks>
internal class ProtectedTokenProvider(IDataProtectionProvider protection, TimeProvider time) : IUserTwoFactorTokenProvider<User>
{
    public const string Name = "Protected";

    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(1);

    private readonly IDataProtector protector = protection.CreateProtector("Tauchplaetze.Accounts.Tokens");

    public async Task<string> GenerateAsync(string purpose, UserManager<User> manager, User user)
    {
        var expires = time.GetUtcNow().Add(Lifetime).ToUnixTimeSeconds();
        return protector.Protect($"{purpose}|{user.Id}|{await manager.GetSecurityStampAsync(user)}|{expires}");
    }

    public async Task<bool> ValidateAsync(string purpose, string token, UserManager<User> manager, User user)
    {
        string[] parts;
        try
        {
            parts = protector.Unprotect(token).Split('|');
        }
        catch (System.Security.Cryptography.CryptographicException)
        {
            return false;
        }

        return parts.Length == 4
               && parts[0] == purpose
               && parts[1] == user.Id
               && parts[2] == await manager.GetSecurityStampAsync(user)
               && long.TryParse(parts[3], out var expires)
               && time.GetUtcNow().ToUnixTimeSeconds() <= expires;
    }

    public Task<bool> CanGenerateTwoFactorTokenAsync(UserManager<User> manager, User user) => Task.FromResult(false);
}
