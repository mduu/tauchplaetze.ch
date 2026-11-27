namespace Tauchplaetze.Web.Features.Susv;

/// <summary>Meldet dem Admin-Layout, dass sich die Zahl der offenen Einträge geändert haben kann. Scoped: eine Instanz pro Verbindung.</summary>
public sealed class SusvReviewSignal
{
    public event Func<Task>? Changed;

    public Task Notify() => Changed?.Invoke() ?? Task.CompletedTask;
}
