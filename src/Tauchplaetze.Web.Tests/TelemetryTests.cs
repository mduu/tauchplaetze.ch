using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Tauchplaetze.Web.Tests;

public class TelemetryTests
{
    [Fact]
    public void Spans_der_Storage_Clients_und_ihre_HTTP_Aufrufe_gehen_nicht_hinaus_andere_Aufrufe_schon()
    {
        // Arrange
        var exported = new List<string>();
        using var queue = new ActivitySource("Azure.Storage.Queues.QueueClient");
        using var http = new ActivitySource("Azure.Core.Http");
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource(queue.Name, http.Name)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new WithoutStorageSpans())
            .AddProcessor(new SimpleActivityExportProcessor(new Collecting(exported)))
            .Build();

        // Act
        using (queue.StartActivity("QueueClient.ReceiveMessages"))
        {
            using (http.StartActivity("GET queue"))
            {
            }
        }

        using (http.StartActivity("GET alplakes"))
        {
        }

        // Assert
        Assert.Equal(["GET alplakes"], exported);
    }

    [Theory]
    [InlineData("Mozilla/5.0 (compatible; SERankingBacklinksBot/1.0; +https://seranking.com/backlinks-crawler)", false)]
    [InlineData("Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)", false)]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/150.0 Safari/537.36", true)]
    public void Unter_dem_Request_eines_Bots_gehen_keine_Spans_hinaus_der_Request_selbst_schon(string userAgent, bool subSpansExported)
    {
        // Arrange
        var exported = new List<string>();
        using var web = new ActivitySource("Microsoft.AspNetCore");
        using var sql = new ActivitySource("OpenTelemetry.Instrumentation.SqlClient");
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource(web.Name, sql.Name)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new WithoutBotSubSpans())
            .AddProcessor(new SimpleActivityExportProcessor(new Collecting(exported)))
            .Build();

        // Act
        using (var request = web.StartActivity("GET /de/tauchplaetze/{Slug}", ActivityKind.Server))
        {
            request?.SetTag("user_agent.original", userAgent);
            using (web.StartActivity("Route /de/tauchplaetze/{Slug}"))
            {
                using (sql.StartActivity("SELECT [DiveSites]", ActivityKind.Client))
                {
                }
            }
        }

        using (sql.StartActivity("UPDATE [DailyForecasts]", ActivityKind.Client))
        {
        }

        // Assert
        string[] expected = subSpansExported
            ? ["SELECT [DiveSites]", "Route /de/tauchplaetze/{Slug}", "GET /de/tauchplaetze/{Slug}", "UPDATE [DailyForecasts]"]
            : ["GET /de/tauchplaetze/{Slug}", "UPDATE [DailyForecasts]"];
        Assert.Equal(expected, exported);
    }

    [Fact]
    public void SQL_Spans_gehen_ohne_Statement_Text_aber_mit_Zusammenfassung_hinaus()
    {
        // Arrange
        var tags = new List<KeyValuePair<string, object?>>();
        using var sql = new ActivitySource("OpenTelemetry.Instrumentation.SqlClient");
        using var tracer = Sdk.CreateTracerProviderBuilder()
            .AddSource(sql.Name)
            .SetSampler(new AlwaysOnSampler())
            .AddProcessor(new WithoutSqlText())
            .AddProcessor(new SimpleActivityExportProcessor(new CollectingTags(tags)))
            .Build();

        // Act
        using (var command = sql.StartActivity("SELECT", ActivityKind.Client))
        {
            command?.SetTag("db.query.summary", "SELECT [DiveSites]");
            command?.SetTag("db.query.text", "SELECT [d].[Id] FROM [DiveSites] AS [d]");
            command?.SetTag("db.statement", "SELECT [d].[Id] FROM [DiveSites] AS [d]");
        }

        // Assert
        Assert.Equal([new("db.query.summary", "SELECT [DiveSites]")], tags);
    }

    private sealed class Collecting(List<string> names) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                names.Add(activity.DisplayName);
            }

            return ExportResult.Success;
        }
    }

    private sealed class CollectingTags(List<KeyValuePair<string, object?>> tags) : BaseExporter<Activity>
    {
        public override ExportResult Export(in Batch<Activity> batch)
        {
            foreach (var activity in batch)
            {
                tags.AddRange(activity.TagObjects);
            }

            return ExportResult.Success;
        }
    }
}
