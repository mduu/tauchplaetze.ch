using Microsoft.Extensions.Options;

namespace Tauchplaetze.Web.CrossCutting.Jobs;

/// <summary>Abschnitt `Jobs`.</summary>
public class JobOptions
{
    public const string Section = "Jobs";

    /// <summary>Pause vor dem ersten Lauf eines Workers nach dem Start. Lokal mit Aspire 0.</summary>
    public TimeSpan StartupGrace { get; set; } = TimeSpan.FromMinutes(2);
}

/// <summary>
/// Pause vor dem ersten Lauf eines Workers nach dem Start der App. Auf dem B1-Plan gehört die CPU zuerst dem Start und dem Warmup
/// (`specs/infrastructure.md`, «Geduld beim Start»).
/// </summary>
internal sealed class StartupGrace(IOptions<JobOptions> options, TimeProvider time)
{
    public Task Wait(CancellationToken ct) =>
        options.Value.StartupGrace > TimeSpan.Zero ? Task.Delay(options.Value.StartupGrace, time, ct) : Task.CompletedTask;
}
