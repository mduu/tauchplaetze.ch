namespace Tauchplaetze.Core.Features.Forecasts;

internal static class ReasonCodes
{
    public static IReadOnlyList<ReasonCode> Parse(string codes) =>
        codes.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => Enum.TryParse<ReasonCode>(code, out var reason) ? reason : (ReasonCode?)null)
            .OfType<ReasonCode>()
            .ToList();
}
