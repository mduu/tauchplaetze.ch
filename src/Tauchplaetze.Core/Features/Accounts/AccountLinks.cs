using System.Buffers.Text;
using System.Text;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>Links in Mails. Die Web-App gibt die Vorlage mit `{user}` und `{token}`, der Core setzt die Werte ein.</summary>
internal static class AccountLinks
{
    public static string Build(string template, string userId, string token) =>
        template.Replace("{user}", Uri.EscapeDataString(userId), StringComparison.Ordinal)
            .Replace("{token}", Base64Url.EncodeToString(Encoding.UTF8.GetBytes(token)), StringComparison.Ordinal);

    public static string? DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(Base64Url.DecodeFromChars(encoded));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
