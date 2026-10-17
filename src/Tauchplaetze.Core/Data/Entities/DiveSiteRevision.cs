using Tauchplaetze.Core.Features.Editorial;

namespace Tauchplaetze.Core.Data.Entities;

internal class DiveSiteRevision
{
    public Guid Id { get; set; }

    public Guid DiveSiteId { get; set; }

    public int Number { get; set; }

    public int? BaseNumber { get; set; }

    public RevisionStatus Status { get; set; }

    public required string SnapshotPath { get; set; }

    public required string SnapshotHash { get; set; }

    public int SnapshotVersion { get; set; }

    public string? Comment { get; set; }

    public string? AuthorId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string? ReviewerId { get; set; }

    public DateTimeOffset? ReviewedAt { get; set; }

    public string? ReviewComment { get; set; }

    public DateTimeOffset? RedactedAt { get; set; }

    public string? RedactedBy { get; set; }
}
