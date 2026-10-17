namespace Tauchplaetze.Core.CrossCutting.UseCases;

/// <summary>Use Case, der Zustand ändert.</summary>
public interface ICommandHandler<in TCommand, TResult>
{
    /// <summary>Führt den Command aus.</summary>
    Task<TResult> Handle(TCommand command, CancellationToken ct = default);
}

/// <summary>Use Case, der nur liest. Eine Query hat nie Seiteneffekte.</summary>
public interface IQueryHandler<in TQuery, TResult>
{
    /// <summary>Beantwortet die Query.</summary>
    Task<TResult> Handle(TQuery query, CancellationToken ct = default);
}

/// <summary>Ergebnis eines Commands ohne Rückgabewert.</summary>
public readonly record struct Unit
{
    /// <summary>Der einzige Wert.</summary>
    public static readonly Unit Value = new();
}
