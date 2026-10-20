using Microsoft.Extensions.Configuration;

namespace Tauchplaetze.Core.CrossCutting.Hosting;

/// <summary>
/// App Service löst Key-Vault-Referenzen in den App Settings auf (infrastructure.md). Fehlt ein Secret oder das Recht,
/// bleibt der Text `@Microsoft.KeyVault(…)` stehen, und die App liefe mit einem falschen Wert. Der Start bricht dann ab.
/// </summary>
public static class KeyVaultReferences
{
    private const string Prefix = "@Microsoft.KeyVault(";

    /// <summary>Wirft, wenn ein Wert noch eine Referenz ist. Die Meldung nennt nur die Keys, nie die Werte.</summary>
    public static void EnsureResolved(IConfiguration configuration)
    {
        var unresolved = configuration.AsEnumerable()
            .Where(e => e.Value?.TrimStart().StartsWith(Prefix, StringComparison.OrdinalIgnoreCase) == true)
            .Select(e => e.Key)
            .Order(StringComparer.Ordinal)
            .ToList();

        if (unresolved.Count > 0)
        {
            throw new InvalidOperationException(
                $"Key Vault references are not resolved: {string.Join(", ", unresolved)}. Check the secret and the role «Key Vault Secrets User» of the app.");
        }
    }
}
