using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHighscores : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "highscore_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Period = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    WolvesvillePlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Username = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    UsernameLower = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Xp = table.Column<long>(type: "bigint", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_highscore_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_highscore_entries_Period_CapturedAt_Rank",
                table: "highscore_entries",
                columns: new[] { "Period", "CapturedAt", "Rank" });

            migrationBuilder.CreateIndex(
                name: "IX_highscore_entries_PlayerId",
                table: "highscore_entries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_highscore_entries_UsernameLower",
                table: "highscore_entries",
                column: "UsernameLower");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "highscore_entries");
        }
    }
}
