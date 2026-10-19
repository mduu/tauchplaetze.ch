namespace Tauchplaetze.Core.Features.Mail;

/// <summary>Regeln der Mail-Queue (ADR-010).</summary>
public static class MailQueueRules
{
    /// <summary>Versuche, bevor eine Mail in die Dead-Letter-Queue geht.</summary>
    public const int MaxAttempts = 5;

    /// <summary>Wartezeit nach dem 1., 2., 3. und 4. gescheiterten Versuch.</summary>
    public static readonly IReadOnlyList<TimeSpan> Backoff = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    /// <summary>So lange ist eine empfangene Mail für andere Worker unsichtbar, während sie gesendet wird.</summary>
    public static readonly TimeSpan Processing = TimeSpan.FromMinutes(2);

    /// <summary>Wartezeit nach einem Fehlschlag mit <paramref name="attempt"/> Versuchen (ab 1).</summary>
    public static TimeSpan DelayAfter(long attempt) => Backoff[(int)Math.Clamp(attempt - 1, 0, Backoff.Count - 1)];
}
