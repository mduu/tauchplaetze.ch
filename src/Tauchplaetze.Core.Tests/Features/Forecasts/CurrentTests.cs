using Tauchplaetze.Core.Features.Forecasts;
using Tauchplaetze.Core.Features.Forecasts.Calculation;
using Tauchplaetze.Core.Features.Forecasts.Sources.Alplakes;

namespace Tauchplaetze.Core.Tests.Features.Forecasts;

public class CurrentTests
{
    private static readonly DateOnly Day = new(2026, 10, 19);

    [Fact]
    public void Die_Stroemung_mittelt_die_Geschwindigkeit_ueber_5_bis_30_m_und_nimmt_den_staerksten_Zeitschritt_im_Tauchfenster()
    {
        // Arrange: Tiefen 2, 5, 30 und 40 m; um 08, 11 und 20 Uhr (MESZ = UTC+2).
        DateTimeOffset[] times = [Utc(6), Utc(9), Utc(18)];
        double[] depths = [2, 5, 30, 40];
        var east = new double?[4, 3];
        var north = new double?[4, 3];
        Set(east, north, 0, [(1, 0), (1, 0), (1, 0)]);
        Set(east, north, 1, [(0, 0.02), (0, 0.06), (0, 0.5)]);
        Set(east, north, 2, [(0, 0.04), (0, 0.10), (0, 0.5)]);
        Set(east, north, 3, [(1, 0), (1, 0), (1, 0)]);
        var series = new DepthTimeSeries(times, depths, new double?[4, 3], 50, east, north);

        // Act
        var current = CurrentEstimate.For(Day, series, DiveWindow.Default, CurrentRules.Default);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(0.08, current.Speed, 6);
        Assert.Equal(0, current.DirectionToward, 6);
        Assert.Equal(CurrentLevel.Noticeable, current.Level);
    }

    [Fact]
    public void Gegenlaeufige_Stroemungen_in_verschiedenen_Tiefen_heben_sich_bei_der_Geschwindigkeit_nicht_auf()
    {
        // Arrange
        var east = new double?[2, 1];
        var north = new double?[2, 1];
        Set(east, north, 0, [(0, 0.1)]);
        Set(east, north, 1, [(0, -0.1)]);
        var series = new DepthTimeSeries([Utc(9)], [10, 20], new double?[2, 1], 50, east, north);

        // Act
        var current = CurrentEstimate.For(Day, series, DiveWindow.Default, CurrentRules.Default);

        // Assert
        Assert.Equal(0.1, current!.Speed, 6);
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(1, 0, 90)]
    [InlineData(0, -1, 180)]
    [InlineData(-1, 0, 270)]
    [InlineData(1, 1, 45)]
    public void Die_Richtung_zeigt_dorthin_wo_das_Wasser_hinfliesst(double east, double north, double expected)
    {
        // Act
        var direction = CurrentEstimate.DirectionToward(east, north);

        // Assert
        Assert.Equal(expected, direction, 6);
    }

    [Theory]
    [InlineData(0.049, CurrentLevel.Calm)]
    [InlineData(0.05, CurrentLevel.Noticeable)]
    [InlineData(0.2, CurrentLevel.Noticeable)]
    [InlineData(0.201, CurrentLevel.Strong)]
    public void Die_Einstufung_ist_ruhig_unter_5_cm_s_und_stark_ueber_20_cm_s(double speed, CurrentLevel expected)
    {
        // Act
        var level = CurrentRules.Default.LevelOf(speed);

        // Assert
        Assert.Equal(expected, level);
    }

    [Fact]
    public void Ohne_Stroemungswerte_im_Tauchfenster_gibt_es_keine_Stroemung()
    {
        // Arrange
        var withoutVelocity = new DepthTimeSeries([Utc(9)], [10], new double?[1, 1], 50);
        var onlyNull = new DepthTimeSeries([Utc(9)], [10], new double?[1, 1], 50, new double?[1, 1], new double?[1, 1]);
        var east = new double?[1, 1];
        var north = new double?[1, 1];
        Set(east, north, 0, [(0.3, 0)]);
        var outsideWindow = new DepthTimeSeries([Utc(18)], [10], new double?[1, 1], 50, east, north);

        // Act
        DayCurrent?[] currents =
        [
            CurrentEstimate.For(Day, withoutVelocity, DiveWindow.Default, CurrentRules.Default),
            CurrentEstimate.For(Day, onlyNull, DiveWindow.Default, CurrentRules.Default),
            CurrentEstimate.For(Day, outsideWindow, DiveWindow.Default, CurrentRules.Default),
            CurrentEstimate.For(Day, null, DiveWindow.Default, CurrentRules.Default),
        ];

        // Assert
        Assert.All(currents, Assert.Null);
    }

    private static DateTimeOffset Utc(int hour) => new(2026, 10, 19, hour, 0, 0, TimeSpan.Zero);

    private static void Set(double?[,] east, double?[,] north, int depth, (double U, double V)[] values)
    {
        for (var time = 0; time < values.Length; time++)
        {
            east[depth, time] = values[time].U;
            north[depth, time] = values[time].V;
        }
    }
}
