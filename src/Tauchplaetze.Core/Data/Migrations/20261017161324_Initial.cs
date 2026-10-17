using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Tauchplaetze.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AspNetRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUsers",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    ShowNamePublicly = table.Column<bool>(type: "bit", nullable: false),
                    PreferredCulture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    BlockedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    BlockedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedUserName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    NormalizedEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    EmailConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SecurityStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ConcurrencyStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneNumberConfirmed = table.Column<bool>(type: "bit", nullable: false),
                    TwoFactorEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LockoutEnd = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    LockoutEnabled = table.Column<bool>(type: "bit", nullable: false),
                    AccessFailedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUsers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WaterBodies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Slug = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    NameDe = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NameFr = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    NameIt = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    WaterBodyType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Outline = table.Column<Geometry>(type: "geography", nullable: true),
                    Centroid = table.Column<Point>(type: "geography", nullable: false),
                    SwissTlmId = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    AlplakesLakeKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AlplakesModel3d = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    AlplakesSimstratKey = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SatellitePoint = table.Column<Point>(type: "geography", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WaterBodies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserLogins",
                columns: table => new
                {
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderKey = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderDisplayName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserLogins", x => new { x.LoginProvider, x.ProviderKey });
                    table.ForeignKey(
                        name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserRoles",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RoleId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserRoles", x => new { x.UserId, x.RoleId });
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiveSites",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    WaterBodyId = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<Point>(type: "geography", nullable: false),
                    Municipality = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Canton = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: true),
                    MaxDepth = table.Column<int>(type: "int", nullable: true),
                    DifficultyRating = table.Column<byte>(type: "tinyint", nullable: true),
                    WallRating = table.Column<byte>(type: "tinyint", nullable: true),
                    MarineLifeRating = table.Column<byte>(type: "tinyint", nullable: true),
                    WrecksRating = table.Column<byte>(type: "tinyint", nullable: true),
                    CurrentRating = table.Column<byte>(type: "tinyint", nullable: true),
                    SurroundingsRating = table.Column<byte>(type: "tinyint", nullable: true),
                    Access = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Features = table.Column<int>(type: "int", nullable: false),
                    HasDivingBan = table.Column<bool>(type: "bit", nullable: false),
                    InflowStationOverride = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    SatellitePoint = table.Column<Point>(type: "geography", nullable: true),
                    ForecastDisabled = table.Column<bool>(type: "bit", nullable: false),
                    DataLevel = table.Column<byte>(type: "tinyint", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ContentLevel = table.Column<byte>(type: "tinyint", nullable: false),
                    HeroMediaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSites", x => x.Id);
                    table.CheckConstraint("CK_DiveSites_ContentLevel", "[ContentLevel] BETWEEN 0 AND 2");
                    table.CheckConstraint("CK_DiveSites_CurrentRating", "[CurrentRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DiveSites_DataLevel", "[DataLevel] BETWEEN 0 AND 3");
                    table.CheckConstraint("CK_DiveSites_DifficultyRating", "[DifficultyRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DiveSites_MarineLifeRating", "[MarineLifeRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DiveSites_SurroundingsRating", "[SurroundingsRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DiveSites_WallRating", "[WallRating] BETWEEN 1 AND 5");
                    table.CheckConstraint("CK_DiveSites_WrecksRating", "[WrecksRating] BETWEEN 1 AND 5");
                    table.ForeignKey(
                        name: "FK_DiveSites_WaterBodies_WaterBodyId",
                        column: x => x.WaterBodyId,
                        principalTable: "WaterBodies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiveSiteRevisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<int>(type: "int", nullable: false),
                    BaseNumber = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SnapshotPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    SnapshotHash = table.Column<string>(type: "nchar(64)", fixedLength: true, maxLength: 64, nullable: false),
                    SnapshotVersion = table.Column<int>(type: "int", nullable: false),
                    Comment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    AuthorId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReviewerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    ReviewComment = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RedactedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    RedactedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiveSiteRevisions_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DiveSiteTranslations",
                columns: table => new
                {
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Culture = table.Column<string>(type: "nchar(2)", fixedLength: true, maxLength: 2, nullable: false),
                    UnderwaterDescription = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SurroundingsInfo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ApproachInfo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    HazardsInfo = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FacilitiesInfo = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DivingBanNote = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsMachineTranslated = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiveSiteTranslations", x => new { x.DiveSiteId, x.Culture });
                    table.ForeignKey(
                        name: "FK_DiveSiteTranslations_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SlugRedirects",
                columns: table => new
                {
                    OldSlug = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DiveSiteId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SlugRedirects", x => x.OldSlug);
                    table.ForeignKey(
                        name: "FK_SlugRedirects_DiveSites_DiveSiteId",
                        column: x => x.DiveSiteId,
                        principalTable: "DiveSites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "AspNetRoles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[,]
                {
                    { "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a01", "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a01", "Contributor", "CONTRIBUTOR" },
                    { "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a02", "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a02", "Moderator", "MODERATOR" },
                    { "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a03", "8b0f6b2e-2f4d-4a59-9c1e-0c3d2a7f1a03", "Admin", "ADMIN" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "RoleNameIndex",
                table: "AspNetRoles",
                column: "NormalizedName",
                unique: true,
                filter: "[NormalizedName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserClaims_UserId",
                table: "AspNetUserClaims",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserLogins_UserId",
                table: "AspNetUserLogins",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUserRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "EmailIndex",
                table: "AspNetUsers",
                column: "NormalizedEmail");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_DisplayName",
                table: "AspNetUsers",
                column: "DisplayName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "UserNameIndex",
                table: "AspNetUsers",
                column: "NormalizedUserName",
                unique: true,
                filter: "[NormalizedUserName] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_DiveSiteRevisions_DiveSiteId_Number",
                table: "DiveSiteRevisions",
                columns: new[] { "DiveSiteId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiveSiteRevisions_DiveSiteId_Status",
                table: "DiveSiteRevisions",
                columns: new[] { "DiveSiteId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DiveSites_Slug",
                table: "DiveSites",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiveSites_WaterBodyId_Status",
                table: "DiveSites",
                columns: new[] { "WaterBodyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SlugRedirects_DiveSiteId",
                table: "SlugRedirects",
                column: "DiveSiteId");

            migrationBuilder.CreateIndex(
                name: "IX_WaterBodies_Slug",
                table: "WaterBodies",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AspNetRoleClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserClaims");

            migrationBuilder.DropTable(
                name: "AspNetUserLogins");

            migrationBuilder.DropTable(
                name: "AspNetUserRoles");

            migrationBuilder.DropTable(
                name: "AspNetUserTokens");

            migrationBuilder.DropTable(
                name: "DiveSiteRevisions");

            migrationBuilder.DropTable(
                name: "DiveSiteTranslations");

            migrationBuilder.DropTable(
                name: "SlugRedirects");

            migrationBuilder.DropTable(
                name: "AspNetRoles");

            migrationBuilder.DropTable(
                name: "AspNetUsers");

            migrationBuilder.DropTable(
                name: "DiveSites");

            migrationBuilder.DropTable(
                name: "WaterBodies");
        }
    }
}
