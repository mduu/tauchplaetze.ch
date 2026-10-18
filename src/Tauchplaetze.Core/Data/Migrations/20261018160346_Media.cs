using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Media : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MediaItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    WaterBodyId = table.Column<int>(type: "int", nullable: true),
                    Section = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntryPointId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    BlobPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Width = table.Column<int>(type: "int", nullable: false),
                    Height = table.Column<int>(type: "int", nullable: false),
                    TakenAt = table.Column<DateOnly>(type: "date", nullable: true),
                    Depth = table.Column<int>(type: "int", nullable: true),
                    Photographer = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    PhotographerUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SourceUrl = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: true),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RightsConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    IsPublic = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaItems_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MediaItems_WaterBodies_WaterBodyId",
                        column: x => x.WaterBodyId,
                        principalTable: "WaterBodies",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "MediaItemTranslations",
                columns: table => new
                {
                    MediaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Caption = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    AltText = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaItemTranslations", x => new { x.MediaItemId, x.Culture });
                    table.ForeignKey(
                        name: "FK_MediaItemTranslations_MediaItems_MediaItemId",
                        column: x => x.MediaItemId,
                        principalTable: "MediaItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_DiveSiteId_Section_SortOrder",
                table: "MediaItems",
                columns: new[] { "DiveSiteId", "Section", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_UploadedBy_UploadedAt",
                table: "MediaItems",
                columns: new[] { "UploadedBy", "UploadedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MediaItems_WaterBodyId",
                table: "MediaItems",
                column: "WaterBodyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MediaItemTranslations");

            migrationBuilder.DropTable(
                name: "MediaItems");
        }
    }
}
