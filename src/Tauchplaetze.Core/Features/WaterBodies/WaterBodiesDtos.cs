namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>Ein Gewässer in einer Liste, mit dem Namen in der gewünschten Sprache, Mittelpunkt für die Karte und Zahl der veröffentlichten Plätze.</summary>
public record WaterBodySummary(int Id, string Slug, string Name, WaterBodyType Type, double Latitude, double Longitude, int SiteCount);
