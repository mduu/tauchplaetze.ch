using Microsoft.Extensions.Configuration;
using Tauchplaetze.Core.CrossCutting.Hosting;

namespace Tauchplaetze.Core.Tests.CrossCutting;

public class KeyVaultReferencesTests
{
    [Fact]
    public void Eine_nicht_aufgeloeste_Referenz_bricht_den_Start_ab_und_die_Meldung_nennt_nur_den_Key()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mail:Password"] = "@Microsoft.KeyVault(VaultName=kv-tauchplaetze-prod;SecretName=Mail--Password)",
            ["Mail:Host"] = "asmtp.mail.hostpoint.ch",
        }).Build();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(() => KeyVaultReferences.EnsureResolved(configuration));

        // Assert
        Assert.Contains("Mail:Password", exception.Message);
        Assert.DoesNotContain("SecretName", exception.Message);
        Assert.DoesNotContain("Mail:Host", exception.Message);
    }

    [Fact]
    public void Aufgeloeste_Werte_lassen_den_Start_zu()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Mail:Password"] = "geheim",
        }).Build();

        // Act
        var exception = Record.Exception(() => KeyVaultReferences.EnsureResolved(configuration));

        // Assert
        Assert.Null(exception);
    }
}
