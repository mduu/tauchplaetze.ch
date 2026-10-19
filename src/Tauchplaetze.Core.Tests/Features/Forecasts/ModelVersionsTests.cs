using Tauchplaetze.Core.Features.Forecasts.Calculation;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class ModelVersionsTests
{
    [Theory]
    [InlineData("rules-1.9,rules-1.10", "rules-1.10")]
    [InlineData("rules-1.3,rules-1.2", "rules-1.3")]
    [InlineData("rules-2.0,rules-1.12", "rules-2.0")]
    public void Die_neueste_Regel_zaehlt_nach_der_Nummer_nicht_nach_dem_Text(string versions, string newest)
    {
        // Act
        var result = ModelVersions.Newest(versions.Split(','));

        // Assert
        Assert.Equal(newest, result);
    }
}
