namespace Tauchplaetze.Core.Features.WaterBodies;

/// <summary>Art eines Gewässers.</summary>
public enum WaterBodyType
{
    /// <summary>See.</summary>
    Lake,

    /// <summary>Fluss.</summary>
    River,
}

/// <summary>Rolle einer BAFU-Station für ein Gewässer.</summary>
public enum HydroStationRole
{
    /// <summary>Zufluss eines Sees.</summary>
    Inflow,

    /// <summary>Station am Fluss selbst, für Flussplätze.</summary>
    River,
}
