using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SusvAndShipping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "RecursYearly",
                table: "DiveSiteNotices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ShippingFeatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    Geometry = table.Column<Geometry>(type: "geography", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceKey = table.Column<string>(type: "varchar(40)", unicode: false, maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShippingFeatures", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SusvEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceId = table.Column<int>(type: "int", nullable: false),
                    Topic = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: false),
                    SourcePublishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ContentHash = table.Column<string>(type: "char(64)", unicode: false, fixedLength: true, maxLength: 64, nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    MissedRuns = table.Column<byte>(type: "tinyint", nullable: false),
                    RemovedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewStatus = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ChangedSinceReview = table.Column<bool>(type: "bit", nullable: false),
                    NoticeKind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidUntil = table.Column<DateOnly>(type: "date", nullable: true),
                    RecursYearly = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    TextsFetchedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SusvEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SusvEntrySites",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SusvEntrySites", x => new { x.EntryId, x.DiveSiteId });
                    table.ForeignKey(
                        name: "FK_SusvEntrySites_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SusvEntrySites_SusvEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "SusvEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SusvEntryTranslations",
                columns: table => new
                {
                    EntryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Teaser = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Text = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    DetailUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsMachineTranslated = table.Column<bool>(type: "bit", nullable: false),
                    ReviewedTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedText = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SusvEntryTranslations", x => new { x.EntryId, x.Culture });
                    table.ForeignKey(
                        name: "FK_SusvEntryTranslations_SusvEntries_EntryId",
                        column: x => x.EntryId,
                        principalTable: "SusvEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ShippingFeatures_Source_SourceKey",
                table: "ShippingFeatures",
                columns: new[] { "Source", "SourceKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SusvEntries_ReviewStatus",
                table: "SusvEntries",
                column: "ReviewStatus");

            migrationBuilder.CreateIndex(
                name: "IX_SusvEntries_SourceId_Topic",
                table: "SusvEntries",
                columns: new[] { "SourceId", "Topic" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SusvEntrySites_DiveSiteId",
                table: "SusvEntrySites",
                column: "DiveSiteId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ShippingFeatures");

            migrationBuilder.DropTable(
                name: "SusvEntrySites");

            migrationBuilder.DropTable(
                name: "SusvEntryTranslations");

            migrationBuilder.DropTable(
                name: "SusvEntries");

            migrationBuilder.DropColumn(
                name: "RecursYearly",
                table: "DiveSiteNotices");
        }
    }
}
