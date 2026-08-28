using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFriendshipsAndExposure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "exposure_assessments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SnapshotId = table.Column<Guid>(type: "uuid", nullable: false),
                    Score = table.Column<int>(type: "integer", nullable: false),
                    CategoryScores = table.Column<string>(type: "jsonb", nullable: false),
                    AssessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exposure_assessments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_exposure_assessments_PlayerId_AssessedAt",
                table: "exposure_assessments",
                columns: new[] { "PlayerId", "AssessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_exposure_assessments_SnapshotId",
                table: "exposure_assessments",
                column: "SnapshotId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "exposure_assessments");
        }
    }
}
