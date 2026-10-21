using System.Text;

namespace Tauchplaetze.Core.Features.Reports;

/// <summary>Kommentar einer Meldung: eine Zeile reiner Text, ohne Steuerzeichen. Leer heisst kein Kommentar.</summary>
internal static class ReportComment
{
    public static string? Normalize(string? comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return null;
        }

        var text = new StringBuilder(comment.Length);
        foreach (var character in comment)
        {
            text.Append(char.IsControl(character) ? ' ' : character);
        }

        return string.Join(' ', text.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
