namespace Tauchplaetze.Core.Features.Shipping;

/// <summary>Art eines Objekts der Schifffahrt (`stories/005-tauchen-und-schifffahrt.md`).</summary>
public enum ShippingFeatureKind
{
    /// <summary>Landungsstelle der Kursschiffe (Punkt). Sporttauchen ist im Umkreis von 100 m verboten.</summary>
    LandingStage,

    /// <summary>Fahrlinie der Kursschiffe (Linie).</summary>
    FerryRoute,

    /// <summary>Hafen (Fläche). Bei der Einfahrt ist Sporttauchen verboten, die Karte zieht 50 m um die Mitte.</summary>
    Harbour,
}

/// <summary>Herkunft eines Objekts der Schifffahrt.</summary>
public enum ShippingSource
{
    /// <summary>BAV, öV-Haltestellen.</summary>
    Bav,

    /// <summary>OpenStreetMap (ODbL).</summary>
    OpenStreetMap,
}
