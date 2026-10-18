using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Sketch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Sketch",
                table: "DiveSites",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Sketch",
                table: "DiveSites");
        }
    }
}
