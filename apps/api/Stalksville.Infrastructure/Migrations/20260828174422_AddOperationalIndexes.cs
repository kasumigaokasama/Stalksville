using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOperationalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_highscore_entries_Period_CapturedAt_Rank",
                table: "highscore_entries");

            migrationBuilder.CreateIndex(
                name: "IX_users_Role",
                table: "users",
                column: "Role");

            migrationBuilder.CreateIndex(
                name: "IX_timeline_events_EventType_OccurredAt",
                table: "timeline_events",
                columns: new[] { "EventType", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_highscore_entries_Period_CapturedAt_Rank",
                table: "highscore_entries",
                columns: new[] { "Period", "CapturedAt", "Rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_api_requests_Endpoint_RequestedAt",
                table: "api_requests",
                columns: new[] { "Endpoint", "RequestedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_users_Role",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_timeline_events_EventType_OccurredAt",
                table: "timeline_events");

            migrationBuilder.DropIndex(
                name: "IX_highscore_entries_Period_CapturedAt_Rank",
                table: "highscore_entries");

            migrationBuilder.DropIndex(
                name: "IX_api_requests_Endpoint_RequestedAt",
                table: "api_requests");

            migrationBuilder.CreateIndex(
                name: "IX_highscore_entries_Period_CapturedAt_Rank",
                table: "highscore_entries",
                columns: new[] { "Period", "CapturedAt", "Rank" });
        }
    }
}
