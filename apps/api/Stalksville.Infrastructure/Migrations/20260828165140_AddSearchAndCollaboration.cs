using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Stalksville.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSearchAndCollaboration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "AssignedToUserId",
                table: "investigations",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "investigations",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_investigations_AssignedToUserId",
                table: "investigations",
                column: "AssignedToUserId");

            // Full-text search support (workspace search): GIN indexes on the FTS expressions the
            // search queries use, plus trigram-free lowercase indexes for identifier matching.
            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS IX_investigations_FT
                    ON investigations USING gin (to_tsvector('simple', "Title" || ' ' || COALESCE("Description", '')));
                CREATE INDEX IF NOT EXISTS IX_investigation_notes_FT
                    ON investigation_notes USING gin (to_tsvector('simple', "Content"));
                CREATE INDEX IF NOT EXISTS IX_clans_NameLower
                    ON clans (LOWER(COALESCE("Name", '')));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS IX_clans_NameLower;
                DROP INDEX IF EXISTS IX_investigation_notes_FT;
                DROP INDEX IF EXISTS IX_investigations_FT;
                """);

            migrationBuilder.DropIndex(
                name: "IX_investigations_AssignedToUserId",
                table: "investigations");

            migrationBuilder.DropColumn(
                name: "AssignedToUserId",
                table: "investigations");

            migrationBuilder.DropColumn(
                name: "Tags",
                table: "investigations");
        }
    }
}
