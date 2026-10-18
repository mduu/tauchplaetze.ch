namespace Tauchplaetze.Core.Features.Forecasts;

/// <summary>Ein Temperaturwert in einer Tiefe.</summary>
internal record ProfilePoint(double Depth, double Temperature);

/// <summary>Ein Temperaturprofil zu einem Zeitpunkt, von der Oberfläche zum Grund sortiert.</summary>
internal record TemperatureProfile(DateTimeOffset Time, IReadOnlyList<ProfilePoint> Points)
{
    public double? SurfaceTemperature => Points.Count > 0 ? Points[0].Temperature : null;

    /// <summary>Die Temperatur in einer Tiefe, linear zwischen den Modellschichten. <c>null</c> ausserhalb des Profils.</summary>
    public double? TemperatureAt(double depth)
    {
        if (Points.Count == 0 || depth < Points[0].Depth - 0.5 || depth > Points[^1].Depth)
        {
            return null;
        }

        if (depth <= Points[0].Depth)
        {
            return Points[0].Temperature;
        }

        var lowerIndex = Enumerable.Range(1, Points.Count - 1).First(i => depth <= Points[i].Depth);
        var upper = Points[lowerIndex - 1];
        var lower = Points[lowerIndex];
        var share = (depth - upper.Depth) / (lower.Depth - upper.Depth);
        return upper.Temperature + share * (lower.Temperature - upper.Temperature);
    }
}
