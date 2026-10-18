using Tauchplaetze.Core.Features.Forecasts;

namespace Tauchplaetze.Core.Data.Entities;

internal class ForecastRun
{
    public Guid Id { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? FinishedAt { get; set; }

    public ForecastRunStatus Status { get; set; }

    public string SourceStatus { get; set; } = "[]";

    public required string RawDataPath { get; set; }
}
