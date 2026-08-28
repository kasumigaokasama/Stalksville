using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAlertReads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "alert_reads",
                columns: table => new
                {
                    AlertId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alert_reads", x => new { x.AlertId, x.UserId });
                });

            migrationBuilder.CreateIndex(
                name: "IX_alert_reads_UserId",
                table: "alert_reads",
                column: "UserId");

            // Read state becomes per user: the legacy global alerts."ReadAt" values move to the
            // seeding admin (the only account that could mark alerts so far), then the column is
            // cleared — the application no longer writes it.
            migrationBuilder.Sql("""
                INSERT INTO alert_reads ("AlertId", "UserId", "ReadAt")
                SELECT a."Id", u."Id", a."ReadAt"
                FROM alerts a
                CROSS JOIN (SELECT "Id" FROM users WHERE "Role" = 'Admin' ORDER BY "CreatedAt" LIMIT 1) u
                WHERE a."ReadAt" IS NOT NULL;
                UPDATE alerts SET "ReadAt" = NULL WHERE "ReadAt" IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort inverse: hand the admin's reads back to the global column.
            migrationBuilder.Sql("""
                UPDATE alerts a SET "ReadAt" = r."ReadAt"
                FROM alert_reads r
                CROSS JOIN (SELECT "Id" FROM users WHERE "Role" = 'Admin' ORDER BY "CreatedAt" LIMIT 1) u
                WHERE r."AlertId" = a."Id" AND r."UserId" = u."Id";
                """);

            migrationBuilder.DropTable(
                name: "alert_reads");
        }
    }
}
