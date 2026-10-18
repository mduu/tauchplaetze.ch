namespace Tauchplaetze.Core.Features.Media;

/// <summary>
/// Speicher der Fotos (`infrastructure.md`, Container). Hochgeladene Dateien liegen privat in `media-pending`.
/// Erst die Freigabe einer Version kopiert die Bildgrössen in den öffentlichen Container `media`. Das Original bleibt privat.
/// </summary>
internal interface IMediaStore
{
    /// <summary>Speichert eine Datei eines Fotos im privaten Container.</summary>
    Task SavePending(Guid mediaId, string name, byte[] content, string contentType, CancellationToken ct);

    /// <summary>Lädt eine Datei aus dem privaten Container. <c>null</c>, wenn es sie nicht gibt.</summary>
    Task<byte[]?> LoadPending(Guid mediaId, string name, CancellationToken ct);

    /// <summary>Kopiert Dateien in den öffentlichen Container. Ein zweiter Aufruf überschreibt sie mit demselben Inhalt.</summary>
    Task Publish(Guid mediaId, IReadOnlyList<string> names, CancellationToken ct);

    /// <summary>Löscht alle privaten Dateien eines Fotos.</summary>
    Task DeletePending(Guid mediaId, CancellationToken ct);

    /// <summary>Die öffentliche Adresse einer Datei.</summary>
    Uri PublicUri(Guid mediaId, string name);
}
