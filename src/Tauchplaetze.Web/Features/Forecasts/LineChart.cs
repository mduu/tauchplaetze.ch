namespace Tauchplaetze.Web.Features.Forecasts;

/// <summary>Ein Punkt einer Reihe. <see cref="X"/> von 0 (links) bis 1 (rechts). <see cref="Raw"/> als blasser Punkt, <see cref="Smooth"/> als Linie.</summary>
public record ChartPoint(double X, double? Raw, double? Smooth);

/// <summary>
/// Eine Reihe im Diagramm. <see cref="Min"/> und <see cref="Max"/> bestimmen die Achse. Die erste Reihe hat ihre Achse links, eine zweite rechts.
/// <see cref="Tone"/> wählt die Farbe (`design-system.md`, «Diagramme»).
/// </summary>
public record ChartSeries(string Tone, string Label, IReadOnlyList<ChartPoint> Points, double Min, double Max, string Unit = "");

/// <summary>Eine Beschriftung auf der Zeitachse oder eine senkrechte Markierung (Regelwechsel).</summary>
public record ChartTick(double X, string Label);
