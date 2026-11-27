using Tauchplaetze.Web.Features.Editorial;

namespace Tauchplaetze.Web.Tests;

public class SwissDateConverterTests
{
    private readonly SwissDateConverter converter = new("ungültig");

    [Theory]
    [InlineData("15.05.2027")]
    [InlineData("15.5.2027")]
    [InlineData(" 15.5.27 ")]
    public void Ein_Datum_liest_sich_auch_ohne_fuehrende_Null_und_mit_zweistelligem_Jahr(string input)
    {
        // Act
        var date = converter.ConvertBack(input);

        // Assert
        Assert.Equal(new DateTime(2027, 5, 15), date);
        Assert.Equal("15.05.2027", converter.Convert(date));
    }

    [Theory]
    [InlineData("31.2.2027")]
    [InlineData("2027-05-15")]
    [InlineData("15.5.")]
    public void Ein_unlesbares_Datum_meldet_einen_Fehler_statt_leer_zu_werden(string input)
    {
        // Act
        var error = Record.Exception(() => converter.ConvertBack(input));

        // Assert
        Assert.IsType<FormatException>(error);
    }
}
