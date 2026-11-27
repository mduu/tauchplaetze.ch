using System.Globalization;
using MudBlazor;

namespace Tauchplaetze.Web.Features.Editorial;

/// <summary>
/// Datum für die Felder im Admin-Bereich: zeigt `dd.MM.yyyy`, liest auch ohne führende Null und mit zweistelligem Jahr («15.5.27»).
/// </summary>
public sealed class SwissDateConverter(string invalidMessage) : IReversibleConverter<DateTime?, string?>
{
    private static readonly string[] Formats = ["d.M.yyyy", "d.M.yy"];

    public string? Convert(DateTime? input) => input?.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);

    public DateTime? ConvertBack(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        return DateTime.TryParseExact(input.Trim(), Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new FormatException(invalidMessage);
    }
}
