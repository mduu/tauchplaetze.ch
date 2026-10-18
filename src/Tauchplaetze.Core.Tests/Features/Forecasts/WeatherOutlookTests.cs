using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.OpenMeteo;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class WeatherOutlookTests
{
    private static readonly DateOnly Today = new(2026, 10, 18);

    [Fact]
    public void Jede_Stunde_nimmt_jeden_Wert_vom_feinsten_Modell_das_ihn_hat()
    {
        // Arrange
        var at9 = Today.ToDateTime(new TimeOnly(9, 0));
        var fine = new ModelWeather("meteoswiss_icon_ch1", [new WeatherHour(at9, 12, 270, 20, null, TemperatureC: 11.04)]);
        var coarse = new ModelWeather("icon_seamless", [
            new WeatherHour(at9, 30, 90, 50, 0.4, TemperatureC: 9, WeatherCode: 61, SunshineSeconds: 1800, IsDay: true),
            new WeatherHour(at9.AddHours(1), 8, 180, 14, 0, TemperatureC: 12)]);

        // Act
        var hours = WeatherOutlook.For(Today, 5, [fine, coarse]);

        // Assert
        Assert.Equal(120, hours.Count);
        Assert.Equal(new WeatherHourForecast(at9, 11.0m, 0.4m, 30, WeatherSymbol.Rain, true, 12m, 20m, 270), hours[9]);
        Assert.Equal(12m, hours[10].TemperatureC);
        Assert.Equal(new WeatherHourForecast(at9.AddHours(2), null, null, null, null, null, null, null, null), hours[11]);
    }

    [Theory]
    [InlineData(0, WeatherSymbol.Clear)]
    [InlineData(2, WeatherSymbol.PartlyCloudy)]
    [InlineData(3, WeatherSymbol.Cloudy)]
    [InlineData(48, WeatherSymbol.Fog)]
    [InlineData(53, WeatherSymbol.Drizzle)]
    [InlineData(65, WeatherSymbol.Rain)]
    [InlineData(81, WeatherSymbol.Showers)]
    [InlineData(75, WeatherSymbol.Snow)]
    [InlineData(86, WeatherSymbol.Snow)]
    [InlineData(95, WeatherSymbol.Thunderstorm)]
    [InlineData(42, null)]
    public void Der_Wettercode_nach_WMO_ergibt_das_Symbol(int code, WeatherSymbol? expected)
    {
        // Act
        var symbol = WeatherSymbols.FromWmo(code);

        // Assert
        Assert.Equal(expected, symbol);
    }
}
