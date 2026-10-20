using Azure.Identity;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Tauchplaetze.Core.CrossCutting.Hosting;

/// <summary>
/// Betrieb in Azure (infrastructure.md): Secrets aus Key Vault, Schlüssel der Data Protection in Blob Storage, geschützt mit einem Key aus Key Vault.
/// Zugriff über die Managed Identity der App. Ohne die Einstellungen (lokal) ohne Wirkung.
/// </summary>
internal static class AzureHosting
{
    public const string KeyVaultUriSetting = "KeyVault:Uri";

    public const string KeysBlobUriSetting = "DataProtection:KeysBlobUri";

    public const string KeyVaultKeyUriSetting = "DataProtection:KeyVaultKeyUri";

    /// <summary>Ein Secret `Mail--Password` erscheint als `Mail:Password`. Fehlt ein Secret, fehlt nur dieser Wert.</summary>
    public static void AddKeyVaultSecrets(IHostApplicationBuilder builder)
    {
        if (builder.Configuration[KeyVaultUriSetting] is { Length: > 0 } uri)
        {
            builder.Configuration.AddAzureKeyVault(new Uri(uri), new DefaultAzureCredential());
        }
    }

    /// <summary>Ohne persistente Schlüssel wären nach jedem Neustart alle Cookies und Links in Mails ungültig.</summary>
    public static void PersistDataProtectionKeys(IHostApplicationBuilder builder)
    {
        if (builder.Configuration[KeysBlobUriSetting] is not { Length: > 0 } blob
            || builder.Configuration[KeyVaultKeyUriSetting] is not { Length: > 0 } key)
        {
            return;
        }

        var credential = new DefaultAzureCredential();
        builder.Services.AddDataProtection()
            .SetApplicationName("tauchplaetze")
            .PersistKeysToAzureBlobStorage(new Uri(blob), credential)
            .ProtectKeysWithAzureKeyVault(new Uri(key), credential);
    }
}
