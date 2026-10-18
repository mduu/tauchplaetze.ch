using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class OtherNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OtherNames",
                table: "DiveSites",
                type: "nvarchar(600)",
                maxLength: 600,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OtherNames",
                table: "DiveSites");
        }
    }
}
