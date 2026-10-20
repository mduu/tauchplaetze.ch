using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Tauchplaetze.Core.Features.Forecasts.Sources.Hydro;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class BafuForecastOptionsTests
{
    [Theory]
    [InlineData("https://www.hydrodata.ch/data/", "user", true)]
    [InlineData("http://www.hydrodata.ch/data/", "user", false)]
    [InlineData("http://www.hydrodata.ch/data/", null, true)]
    [InlineData("www.hydrodata.ch/data/", "user", false)]
    public void Zugangsdaten_gehen_nur_ueber_HTTPS_hinaus_sonst_bricht_der_Start_ab(string baseUrl, string? username, bool valid)
    {
        // Arrange
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["BaseUrl"] = baseUrl,
            ["Username"] = username,
            ["Password"] = username is null ? null : "secret",
        }).Build();
        var services = new ServiceCollection();
        BafuForecastOptions.Register(services, configuration);
        using var provider = services.BuildServiceProvider();

        // Act
        var exception = Record.Exception(() => provider.GetRequiredService<IOptions<BafuForecastOptions>>().Value);

        // Assert
        Assert.Equal(valid, exception is null);
        Assert.True(valid || exception is OptionsValidationException);
    }
}
