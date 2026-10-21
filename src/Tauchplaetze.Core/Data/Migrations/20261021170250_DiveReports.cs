using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class DiveReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Visibility = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: false),
                    Temperature = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    TemperatureDepth = table.Column<int>(type: "int", nullable: true),
                    Comment = table.Column<string>(type: "nvarchar(280)", maxLength: 280, nullable: true),
                    CommentStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    HiddenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    HiddenBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ForecastIndex = table.Column<decimal>(type: "decimal(3,1)", precision: 3, scale: 1, nullable: true),
                    ForecastModelVersion = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_AuthorId_CreatedAt",
                table: "Reports",
                columns: new[] { "AuthorId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Reports_CommentStatus",
                table: "Reports",
                column: "CommentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DiveSiteId_AuthorId_Date",
                table: "Reports",
                columns: new[] { "DiveSiteId", "AuthorId", "Date" },
                unique: true,
                filter: "[AuthorId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_DiveSiteId_Date",
                table: "Reports",
                columns: new[] { "DiveSiteId", "Date" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Reports");
        }
    }
}
