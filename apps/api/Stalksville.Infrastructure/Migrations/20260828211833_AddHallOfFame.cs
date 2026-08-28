using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHallOfFame : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "hall_of_fame_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SeasonNumber = table.Column<int>(type: "integer", nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    WolvesvillePlayerId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PlayerNameLower = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    AvatarUrl = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: true),
                    CapturedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hall_of_fame_entries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_hall_of_fame_entries_PlayerId",
                table: "hall_of_fame_entries",
                column: "PlayerId");

            migrationBuilder.CreateIndex(
                name: "IX_hall_of_fame_entries_PlayerNameLower",
                table: "hall_of_fame_entries",
                column: "PlayerNameLower");

            migrationBuilder.CreateIndex(
                name: "IX_hall_of_fame_entries_SeasonNumber_Position",
                table: "hall_of_fame_entries",
                columns: new[] { "SeasonNumber", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "hall_of_fame_entries");
        }
    }
}
