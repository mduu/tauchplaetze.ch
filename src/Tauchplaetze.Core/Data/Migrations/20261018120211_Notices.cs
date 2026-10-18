using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Notices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DiveSiteNotices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteNotices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiveSiteNotices_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiveSiteNoticeTranslations",
                columns: table => new
                {
                    NoticeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteNoticeTranslations", x => new { x.NoticeId, x.Culture });
                    table.ForeignKey(
                        name: "FK_DiveSiteNoticeTranslations_DiveSiteNotices_NoticeId",
                        column: x => x.NoticeId,
                        principalTable: "DiveSiteNotices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DiveSiteNotices_DiveSiteId_SortOrder",
                table: "DiveSiteNotices",
                columns: new[] { "DiveSiteId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DiveSiteNoticeTranslations");

            migrationBuilder.DropTable(
                name: "DiveSiteNotices");
        }
    }
}
