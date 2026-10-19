using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class LakeCurrent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CurrentDirection",
                table: "DailyForecasts",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentLevel",
                table: "DailyForecasts",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CurrentSpeed",
                table: "DailyForecasts",
                type: "decimal(5,3)",
                precision: 5,
                scale: 3,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CurrentDirection",
                table: "DailyForecasts");

            migrationBuilder.DropColumn(
                name: "CurrentLevel",
                table: "DailyForecasts");

            migrationBuilder.DropColumn(
                name: "CurrentSpeed",
                table: "DailyForecasts");
        }
    }
}
