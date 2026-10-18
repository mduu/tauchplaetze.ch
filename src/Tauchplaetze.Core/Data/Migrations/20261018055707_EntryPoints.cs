using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class EntryPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiveSiteEntryPoints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteEntryPoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiveSiteEntryPoints_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiveSiteEntryPointTranslations",
                columns: table => new
                {
                    EntryPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteEntryPointTranslations", x => new { x.EntryPointId, x.Culture });
                    table.ForeignKey(
                        name: "FK_DiveSiteEntryPointTranslations_DiveSiteEntryPoints_EntryPointId",
                        column: x => x.EntryPointId,
                        principalTable: "DiveSiteEntryPoints",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiveSiteEntryPoints_DiveSiteId_SortOrder",
                table: "DiveSiteEntryPoints",
                columns: new[] { "DiveSiteId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiveSiteEntryPointTranslations");

            migrationBuilder.DropTable(
                name: "DiveSiteEntryPoints");
        }
    }
}
