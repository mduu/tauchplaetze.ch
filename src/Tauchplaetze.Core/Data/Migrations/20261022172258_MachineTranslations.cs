using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class MachineTranslations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MachineTranslations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    SourceCulture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    TargetCulture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    SourceHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Model = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MachineTranslations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MachineTranslations_CreatedAt",
                table: "MachineTranslations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MachineTranslations_RequestedBy_CreatedAt",
                table: "MachineTranslations",
                columns: new[] { "RequestedBy", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MachineTranslations_ResultHash",
                table: "MachineTranslations",
                column: "ResultHash");

            migrationBuilder.CreateIndex(
                name: "IX_MachineTranslations_SourceHash",
                table: "MachineTranslations",
                column: "SourceHash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MachineTranslations");
        }
    }
}
