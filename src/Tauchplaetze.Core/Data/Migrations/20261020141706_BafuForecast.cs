using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class BafuForecast : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForecastFetchedAt",
                table: "HydroReadings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ForecastIssuedAt",
                table: "HydroReadings",
                type: "datetimeoffset",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ForecastOfficial",
                table: "HydroReadings",
                type: "bit",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ForecastFetchedAt",
                table: "HydroReadings");

            migrationBuilder.DropColumn(
                name: "ForecastIssuedAt",
                table: "HydroReadings");

            migrationBuilder.DropColumn(
                name: "ForecastOfficial",
                table: "HydroReadings");
        }
    }
}
