using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Discharge : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "Location",
                table: "WaterBodyHydroStations",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DischargeLowFrom",
                table: "DiveSites",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DischargeMediumFrom",
                table: "DiveSites",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DischargeStrongFrom",
                table: "DiveSites",
                type: "decimal(7,1)",
                precision: 7,
                scale: 1,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HydroHours",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Hour = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Discharge = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HydroHours", x => new { x.StationId, x.Kind, x.Hour });
                });

            migrationBuilder.CreateTable(
                name: "HydroReadings",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Discharge = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: false),
                    DischargeThreeHoursBefore = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    Median30Days = table.Column<decimal>(type: "decimal(8,2)", precision: 8, scale: 2, nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HydroReadings", x => x.StationId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HydroHours_Hour",
                table: "HydroHours",
                column: "Hour");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HydroHours");

            migrationBuilder.DropTable(
                name: "HydroReadings");

            migrationBuilder.DropColumn(
                name: "Location",
                table: "WaterBodyHydroStations");

            migrationBuilder.DropColumn(
                name: "DischargeLowFrom",
                table: "DiveSites");

            migrationBuilder.DropColumn(
                name: "DischargeMediumFrom",
                table: "DiveSites");

            migrationBuilder.DropColumn(
                name: "DischargeStrongFrom",
                table: "DiveSites");
        }
    }
}
