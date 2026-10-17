namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Speicher für den Inhalt der Versionen eines Tauchplatzes (Blob Storage, Container `revisions`, ADR-008).</summary>
internal interface IRevisionStore
{
    /// <summary>Speichert den Inhalt einer Version. Ein vorhandener Blob mit demselben Pfad wird überschrieben.</summary>
    Task Save(string path, byte[] content, CancellationToken ct);

    /// <summary>Lädt den Inhalt einer Version.</summary>
    Task<byte[]> Load(string path, CancellationToken ct);
}
