using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class RevisionAuthorIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_DiveSiteRevisions_AuthorId_Status",
                table: "DiveSiteRevisions",
                columns: new[] { "AuthorId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_DiveSiteRevisions_AuthorId_Status",
                table: "DiveSiteRevisions");
        }
    }
}
