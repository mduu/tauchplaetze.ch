namespace Tauchplaetze.Core.Tests.Features.Forecasts;

internal static class TestData
{
    public static string Read(string source, string file) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "TestData", source, file));
}
