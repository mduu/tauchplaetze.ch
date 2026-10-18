namespace Tauchplaetze.Core.CrossCutting.Storage;

/// <summary>Speicher für die unveränderten Antworten der Quellen (Container `forecast-raw`). Damit lassen sich alte Läufe neu berechnen.</summary>
internal interface IRawDataStore
{
    /// <summary>Speichert eine Antwort unter <paramref name="path"/>.</summary>
    Task Save(string path, string content, CancellationToken ct);
}
