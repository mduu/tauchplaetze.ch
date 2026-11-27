namespace Tauchplaetze.Core.Features.Shipping;

/// <summary>Abstände der Ebene «Schifffahrt» (`stories/005-tauchen-und-schifffahrt.md`). Jeder Abstand gilt ab der Mitte des Objekts (Entscheid Marc, 25.11.2026).</summary>
public static class ShippingRules
{
    /// <summary>Art. 77 BSV: kein Sporttauchen im Umkreis von 100 m um Landungsstellen der Kursschiffe.</summary>
    public const double LandingStageRadiusMeters = 100;

    /// <summary>Um die Mitte eines Hafens und eines Bojenfelds. Die Verordnung nennt keinen Wert, der SUSV arbeitet mit 50 m.</summary>
    public const double CentreRadiusMeters = 50;

    /// <summary>Die Karte zeigt Objekte in diesem Umkreis um den Platz und seine Einstiege.</summary>
    public const double NearbyMeters = 500;

    public const int NameLength = 120;

    public const int SourceKeyLength = 40;
}
