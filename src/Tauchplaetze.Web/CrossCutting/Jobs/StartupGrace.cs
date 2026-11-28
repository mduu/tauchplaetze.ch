namespace Tauchplaetze.Web.CrossCutting.Jobs;

/// <summary>
/// Pause vor dem ersten Lauf eines Workers nach dem Start der App. Auf dem B1-Plan gehört die CPU zuerst dem Start und dem Warmup
/// (`specs/infrastructure.md`, «Geduld beim Start»).
/// </summary>
internal static class StartupGrace
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(2);

    public static Task Wait(TimeProvider time, CancellationToken ct) => Task.Delay(Duration, time, ct);
}
