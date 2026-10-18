namespace Tauchplaetze.Core.Features.Editorial;

/// <summary>Regeln für die weiteren Namen eines Platzes (`dive-site.md`, «Auch bekannt als»).</summary>
public static class OtherNames
{
    /// <summary>Höchstens so viele weitere Namen.</summary>
    public const int MaxCount = 5;

    /// <summary>Länge eines Namens.</summary>
    public const int MaxLength = 80;

    /// <summary>Getrimmt, ohne leere Einträge, Reihenfolge wie erfasst.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string?> names) =>
        [.. names.Select(n => n?.Trim()).OfType<string>().Where(n => n.Length > 0)];

    internal static bool AreInvalid(string name, IReadOnlyList<string> otherNames)
    {
        var names = Normalize(otherNames);
        return names.Count > MaxCount
            || names.Any(n => n.Length > MaxLength)
            || names.Append(name.Trim()).DistinctBy(n => n.ToLowerInvariant()).Count() != names.Count + 1;
    }
}
