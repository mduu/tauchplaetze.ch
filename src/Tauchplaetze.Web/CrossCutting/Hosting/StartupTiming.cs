using System.Diagnostics;

namespace Tauchplaetze.Web.CrossCutting.Hosting;

/// <summary>
/// Schreibt die Dauer der Startphasen ins Log, damit ein langsamer Start nach dem Deploy messbar ist (`specs/infrastructure.md`, «Geduld beim Start»).
/// Die Zeilen gehen auch auf die Konsole: App Insights erhält vor «Now listening» oft nichts.
/// </summary>
internal static class StartupTiming
{
    /// <summary>Eigene Kategorie: In Prod schreibt die Konsole sonst nur Warnungen (`appsettings.Production.json`).</summary>
    public const string Category = "Tauchplaetze.Startup";

    public static void LogPhase(this WebApplication app, string phase) =>
        Logger(app).LogInformation("Startup phase {Phase} reached after {SinceProcessStart:F1} s", phase, SinceProcessStart().TotalSeconds);

    public static void LogWhenReady(this WebApplication app) =>
        app.Lifetime.ApplicationStarted.Register(() => app.LogPhase("Ready"));

    public static async Task<T> Timed<T>(this WebApplication app, string step, Func<Task<T>> run)
    {
        var started = TimeProvider.System.GetTimestamp();
        var result = await run();
        Logger(app).LogInformation("Startup step {Step} took {Duration:F1} s", step, TimeProvider.System.GetElapsedTime(started).TotalSeconds);
        return result;
    }

    private static ILogger Logger(WebApplication app) => app.Services.GetRequiredService<ILoggerFactory>().CreateLogger(Category);

    private static TimeSpan SinceProcessStart()
    {
        using var process = Process.GetCurrentProcess();
        return DateTime.Now - process.StartTime;
    }
}
