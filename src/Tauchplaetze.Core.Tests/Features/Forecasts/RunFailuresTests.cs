using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class RunFailuresTests
{
    [Theory]
    [InlineData(null, "The operation didn't complete within the allowed timeout of '00:01:00'.", FailureKind.Timeout)]
    [InlineData(null, "The request was canceled due to the configured HttpClient.Timeout of 240 seconds elapsing.", FailureKind.Timeout)]
    [InlineData(null, "The circuit is now open and is not allowing calls.", FailureKind.CircuitOpen)]
    [InlineData(null, "No such host is known.", FailureKind.Network)]
    [InlineData(503, "Service Unavailable", FailureKind.Http)]
    [InlineData(200, "Parse: JsonException: unexpected end", FailureKind.Parse)]
    public void Ein_gescheiterter_Call_gehoert_zu_einer_Art_Timeout_Circuit_Netz_HTTP_oder_Lesen(int? statusCode, string error, FailureKind expected)
    {
        // Act
        var kind = RunFailures.KindOf(new SourceCallStatus("alplakes", "k", false, statusCode, error, 0));

        // Assert
        Assert.Equal(expected, kind);
    }
}
