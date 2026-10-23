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
}
