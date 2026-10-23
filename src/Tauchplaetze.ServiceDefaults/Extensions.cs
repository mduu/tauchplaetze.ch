using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds the common Aspire services: OpenTelemetry, health checks, service discovery and HTTP resilience.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    private static readonly string[] StaticPathPrefixes = ["/_framework", "/_content", "/_blazor", "/bathymetry"];

    private static readonly string[] StaticFileExtensions = [".css", ".js", ".woff2", ".png", ".webp", ".ico", ".svg"];

    private static readonly string[] InstrumentedMeters = ["Microsoft.AspNetCore", "System.", "OpenTelemetry.Instrumentation"];

    private static readonly HashSet<string> KeptMetrics = ["http.server.request.duration", "http.client.request.duration", "dotnet.gc.last_collection.heap.size"];

    /// <summary>
    /// Adds the service defaults to the application.
    /// </summary>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();
        builder.AddDefaultHealthChecks();
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            http.AddStandardResilienceHandler();
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Configures logging, metrics and tracing with OpenTelemetry.
    /// </summary>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddView(instrument => IsKept(instrument) ? null : MetricStreamConfiguration.Drop);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(options => options.Filter = context => IsTraced(context.Request.Path))
                    .AddHttpClientInstrumentation()
                    .AddProcessor(new WithoutStorageSpans());
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>
    /// Health Checks, statische Dateien und die Blazor-Verbindung erzeugen keinen Trace. Sie füllen sonst das Tageslimit von Application Insights.
    /// </summary>
    private static bool IsTraced(PathString path) =>
        !path.StartsWithSegments(HealthEndpointPath)
        && !path.StartsWithSegments(AlivenessEndpointPath)
        && !StaticPathPrefixes.Any(prefix => path.StartsWithSegments(prefix))
        && !StaticFileExtensions.Contains(Path.GetExtension(path.Value ?? string.Empty), StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Von den Metriken der Instrumentierung bleiben nur Dauer der Requests, Dauer der ausgehenden Aufrufe und Heap. Keine Ansicht liest die übrigen,
    /// sie machten mehr als die Hälfte des Tageslimits aus (`infrastructure.md`, «Betrieb»).
    /// </summary>
    private static bool IsKept(Instrument instrument) =>
        KeptMetrics.Contains(instrument.Name) || !InstrumentedMeters.Any(prefix => instrument.Meter.Name.StartsWith(prefix, StringComparison.Ordinal));

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);
        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        if (!string.IsNullOrWhiteSpace(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        {
            var samplingRatio = float.TryParse(builder.Configuration["Telemetry:SamplingRatio"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var ratio) ? ratio : 1.0f;
            builder.Services.AddOpenTelemetry().UseAzureMonitor(options =>
            {
                options.TracesPerSecond = null;
                options.SamplingRatio = samplingRatio;
                options.EnableTraceBasedLogsSampler = false;
            });
        }

        return builder;
    }

    /// <summary>
    /// Adds a liveness health check.
    /// </summary>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>
    /// Maps `/health` and `/alive`. Both run only the liveness check, without a database call: App Service polls `/alive` every minute.
    /// </summary>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        var live = new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") };
        app.MapHealthChecks(HealthEndpointPath, live);
        var commit = BuildCommit(Assembly.Load(app.Environment.ApplicationName));
        app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
        {
            Predicate = live.Predicate,
            ResponseWriter = (context, report) => WriteAliveness(context, report, commit),
        });

        return app;
    }

    /// <summary>
    /// The commit of a build: the part after `+` in the informational version (`SourceRevisionId` at publish). Null without a commit.
    /// `/alive` sends it in the header `X-Build`. The deploy workflow waits until the new commit answers.
    /// </summary>
    public static string? BuildCommit(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        var plus = version?.IndexOf('+') ?? -1;
        return plus >= 0 && plus < version!.Length - 1 ? version[(plus + 1)..] : null;
    }

    private static Task WriteAliveness(HttpContext context, HealthReport report, string? commit)
    {
        if (commit is not null)
        {
            context.Response.Headers["X-Build"] = commit;
        }

        context.Response.ContentType = "text/plain";
        return context.Response.WriteAsync(report.Status.ToString());
    }
}

/// <summary>
/// Spans der Storage-Clients (Mail-Queue, Blobs, Lease) und ihre HTTP-Aufrufe gehen nicht an Application Insights.
/// Die Mail-Queue fragt alle paar Sekunden ab, keine Ansicht liest diese Spans. Fehler kommen weiterhin als Log.
/// </summary>
public sealed class WithoutStorageSpans : BaseProcessor<Activity>
{
    public override void OnStart(Activity data)
    {
        if (IsStorage(data))
        {
            data.IsAllDataRequested = false;
            data.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
        }
    }

    private static bool IsStorage(Activity? activity) =>
        activity is not null && (activity.Source.Name.StartsWith("Azure.Storage.", StringComparison.Ordinal) || IsStorage(activity.Parent));
}
