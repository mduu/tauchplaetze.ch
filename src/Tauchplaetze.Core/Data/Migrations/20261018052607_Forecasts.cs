using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Forecasts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DailyForecasts",
                columns: table => new
                {
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    DataLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    DiveIndex = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    ProfileSource = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SurfaceTemperature = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    TemperatureAt10m = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    TemperatureAt20m = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    TemperatureAt30m = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    ThermoclineTop = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    ThermoclineBottom = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    SecchiDepth = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    SecchiObservedAt = table.Column<DateOnly>(type: "date", nullable: true),
                    VisibilityIndicator = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    VisibilityGoodBelow = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    WindBeaufort = table.Column<int>(type: "int", nullable: true),
                    WindSpeedKmh = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    WindGustsKmh = table.Column<decimal>(type: "decimal(4,1)", precision: 4, scale: 1, nullable: true),
                    WindDirection = table.Column<int>(type: "int", nullable: true),
                    WeatherModel = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    PrecipitationLast72h = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    PrecipitationOnDay = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: true),
                    InflowDischargeRatio = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: true),
                    Turbidity = table.Column<decimal>(type: "decimal(6,1)", precision: 6, scale: 1, nullable: true),
                    AlgaeLevel = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReasonCodes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ModelVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ForecastRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ComputedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyForecasts", x => new { x.DiveSiteId, x.Date });
                    table.CheckConstraint("CK_DailyForecasts_DataLevel", "[DataLevel] BETWEEN 0 AND 3");
                    table.ForeignKey(
                        name: "FK_DailyForecasts_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ForecastRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceStatus = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RawDataPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ForecastRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterBodyHydroStations",
                columns: table => new
                {
                    WaterBodyId = table.Column<int>(type: "int", nullable: false),
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Weight = table.Column<decimal>(type: "decimal(4,2)", precision: 4, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterBodyHydroStations", x => new { x.WaterBodyId, x.StationId });
                    table.ForeignKey(
                        name: "FK_WaterBodyHydroStations_WaterBodies_WaterBodyId",
                        column: x => x.WaterBodyId,
                        principalTable: "WaterBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DailyForecasts_Date_DiveIndex",
                table: "DailyForecasts",
                columns: new[] { "Date", "DiveIndex" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ForecastRuns_StartedAt",
                table: "ForecastRuns",
                column: "StartedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DailyForecasts");

            migrationBuilder.DropTable(
                name: "ForecastRuns");

            migrationBuilder.DropTable(
                name: "WaterBodyHydroStations");
        }
    }
}
