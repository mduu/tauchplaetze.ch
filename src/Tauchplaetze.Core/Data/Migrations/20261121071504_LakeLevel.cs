using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class LakeLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LakeLevelReadings",
                columns: table => new
                {
                    StationId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    MeasuredAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Level = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: false),
                    NormDay = table.Column<DateOnly>(type: "date", nullable: true),
                    NormLow = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    NormMedian = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    NormHigh = table.Column<decimal>(type: "decimal(7,3)", precision: 7, scale: 3, nullable: true),
                    NormFetchedOn = table.Column<DateOnly>(type: "date", nullable: true),
                    ImportedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LakeLevelReadings", x => x.StationId);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LakeLevelReadings");
        }
    }
}
