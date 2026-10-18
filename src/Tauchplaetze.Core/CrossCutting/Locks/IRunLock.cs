namespace Tauchplaetze.Core.CrossCutting.Locks;

/// <summary>Ein Lock über alle Instanzen. Nur ein Lauf eines Jobs gleichzeitig (`forecast.md`, «Der Job»).</summary>
internal interface IRunLock
{
    /// <summary>Holt den Lock <paramref name="name"/>. <c>null</c>, wenn ein anderer Lauf ihn hält. Dispose gibt ihn frei.</summary>
    Task<IAsyncDisposable?> TryAcquire(string name, CancellationToken ct);
}
