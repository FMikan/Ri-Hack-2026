using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RankedQueue : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "veto",
                table: "matches",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_after",
                table: "match_players",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating_before",
                table: "match_players",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "match_proposals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_a = table.Column<Guid>(type: "uuid", nullable: false),
                    user_b = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    a_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    b_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_proposals", x => x.id);
                    table.CheckConstraint("ck_match_proposals_status", "status in ('pending', 'matched', 'expired', 'declined')");
                    table.ForeignKey(
                        name: "fk_match_proposals_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_match_proposals_users_user_a",
                        column: x => x.user_a,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_proposals_users_user_b",
                        column: x => x.user_b,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "queue_entries",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rating = table.Column<int>(type: "integer", nullable: false),
                    topic_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    entered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    proposal_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_queue_entries", x => x.user_id);
                    table.CheckConstraint("ck_queue_entries_status", "status in ('waiting', 'proposed')");
                    table.ForeignKey(
                        name: "fk_queue_entries_match_proposals_proposal_id",
                        column: x => x.proposal_id,
                        principalTable: "match_proposals",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_queue_entries_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_match_proposals_expires_at",
                table: "match_proposals",
                column: "expires_at",
                filter: "status = 'pending'");

            migrationBuilder.CreateIndex(
                name: "ix_match_proposals_match_id",
                table: "match_proposals",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_proposals_user_a",
                table: "match_proposals",
                column: "user_a");

            migrationBuilder.CreateIndex(
                name: "ix_match_proposals_user_b",
                table: "match_proposals",
                column: "user_b");

            migrationBuilder.CreateIndex(
                name: "ix_queue_entries_proposal_id",
                table: "queue_entries",
                column: "proposal_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "queue_entries");

            migrationBuilder.DropTable(
                name: "match_proposals");

            migrationBuilder.DropColumn(
                name: "veto",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "rating_after",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "rating_before",
                table: "match_players");
        }
    }
}
