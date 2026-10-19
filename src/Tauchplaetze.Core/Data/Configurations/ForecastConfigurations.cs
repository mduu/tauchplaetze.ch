using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Tauchplaetze.Core.Data.Entities;

namespace Tauchplaetze.Core.Data.Configurations;

internal class DailyForecastConfiguration : IEntityTypeConfiguration<DailyForecast>
{
    public void Configure(EntityTypeBuilder<DailyForecast> builder)
    {
        builder.ToTable("DailyForecasts", table => table.HasCheckConstraint("CK_DailyForecasts_DataLevel", "[DataLevel] BETWEEN 0 AND 3"));
        builder.HasKey(f => new { f.DiveSiteId, f.Date });
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(f => f.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(f => new { f.Date, f.DiveIndex }).IsDescending(false, true);

        builder.Property(f => f.DiveIndex).HasPrecision(3, 1);
        foreach (var property in new[] { nameof(DailyForecast.SurfaceTemperature), nameof(DailyForecast.TemperatureAt10m), nameof(DailyForecast.TemperatureAt20m),
                     nameof(DailyForecast.TemperatureAt30m), nameof(DailyForecast.ThermoclineTop), nameof(DailyForecast.ThermoclineBottom),
                     nameof(DailyForecast.SecchiDepth), nameof(DailyForecast.SecchiDepthShore), nameof(DailyForecast.VisibilityGoodBelow), nameof(DailyForecast.WindSpeedKmh), nameof(DailyForecast.WindGustsKmh) })
        {
            builder.Property(property).HasPrecision(4, 1);
        }

        builder.Property(f => f.PrecipitationLast72h).HasPrecision(5, 1);
        builder.Property(f => f.PrecipitationOnDay).HasPrecision(5, 1);
        builder.Property(f => f.InflowDischargeRatio).HasPrecision(5, 2);
        builder.Property(f => f.Turbidity).HasPrecision(6, 1);
        builder.Property(f => f.WeatherModel).HasMaxLength(40);
        builder.Property(f => f.CurrentSpeed).HasPrecision(5, 3);
        builder.Property(f => f.ReasonCodes).HasMaxLength(200);
        builder.Property(f => f.ModelVersion).HasMaxLength(20);
    }
}

internal class ForecastRunConfiguration : IEntityTypeConfiguration<ForecastRun>
{
    public void Configure(EntityTypeBuilder<ForecastRun> builder)
    {
        builder.ToTable("ForecastRuns");
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.RawDataPath).HasMaxLength(300);
        builder.HasIndex(r => r.StartedAt);
    }
}

internal class WaterBodyHydroStationConfiguration : IEntityTypeConfiguration<WaterBodyHydroStation>
{
    public void Configure(EntityTypeBuilder<WaterBodyHydroStation> builder)
    {
        builder.ToTable("WaterBodyHydroStations");
        builder.HasKey(s => new { s.WaterBodyId, s.StationId });
        builder.HasOne<WaterBody>().WithMany(w => w.HydroStations).HasForeignKey(s => s.WaterBodyId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(s => s.StationId).HasMaxLength(20);
        builder.Property(s => s.Name).HasMaxLength(100);
        builder.Property(s => s.Weight).HasPrecision(4, 2);
        builder.Property(s => s.Location).HasColumnType("geography");
    }
}

internal class HydroHourConfiguration : IEntityTypeConfiguration<HydroHour>
{
    public void Configure(EntityTypeBuilder<HydroHour> builder)
    {
        builder.ToTable("HydroHours");
        builder.HasKey(h => new { h.StationId, h.Kind, h.Hour });
        builder.HasIndex(h => h.Hour);
        builder.Property(h => h.StationId).HasMaxLength(20);
        builder.Property(h => h.Discharge).HasPrecision(8, 2);
    }
}

internal class HydroReadingConfiguration : IEntityTypeConfiguration<HydroReading>
{
    public void Configure(EntityTypeBuilder<HydroReading> builder)
    {
        builder.ToTable("HydroReadings");
        builder.HasKey(r => r.StationId);
        builder.Property(r => r.StationId).HasMaxLength(20);
        builder.Property(r => r.Discharge).HasPrecision(8, 2);
        builder.Property(r => r.DischargeThreeHoursBefore).HasPrecision(8, 2);
        builder.Property(r => r.Median30Days).HasPrecision(8, 2);
        builder.Property(r => r.Temperature).HasPrecision(4, 1);
    }
}

internal class SiteWeatherOutlookConfiguration : IEntityTypeConfiguration<SiteWeatherOutlook>
{
    public void Configure(EntityTypeBuilder<SiteWeatherOutlook> builder)
    {
        builder.ToTable("SiteWeatherOutlooks");
        builder.HasKey(o => o.DiveSiteId);
        builder.HasOne<DiveSite>().WithMany().HasForeignKey(o => o.DiveSiteId).OnDelete(DeleteBehavior.Cascade);
    }
}
