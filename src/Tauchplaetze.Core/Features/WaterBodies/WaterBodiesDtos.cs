namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>Ein Gewässer in einer Liste, mit dem Namen in der gewünschten Sprache.</summary>
public record WaterBodySummary(int Id, string Slug, string Name, WaterBodyType Type);
