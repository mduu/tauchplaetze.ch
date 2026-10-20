using System.Collections.Concurrent;

namespace Tauchplaetze.Core.Features.Accounts;

/// <summary>
/// Höchstens eine Mail derselben Art pro Konto innert <see cref="Cooldown"/>. Arten: <see cref="Registration"/> (Bestätigung oder «Konto besteht schon») und <see cref="PasswordReset"/>.
/// Schützt das Postfach vor Mail-Bombing. Zählt nur im Arbeitsspeicher (eine Instanz, `infrastructure.md`).
/// </summary>
internal class AccountMailCooldown(TimeProvider time)
{
    public const string Registration = "registration";

    public const string PasswordReset = "password-reset";

    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(15);

    private readonly ConcurrentDictionary<string, DateTimeOffset> lastSent = new();

    /// <summary>True, wenn für das Konto eine Mail dieser Art gehen darf. Merkt sich dann den Zeitpunkt.</summary>
    public bool TryStart(string userId, string kind)
    {
        var now = time.GetUtcNow();
        var allowed = true;
        lastSent.AddOrUpdate($"{kind}:{userId}", now, (_, previous) =>
        {
            allowed = now - previous >= Cooldown;
            return allowed ? now : previous;
        });
        return allowed;
    }
}
