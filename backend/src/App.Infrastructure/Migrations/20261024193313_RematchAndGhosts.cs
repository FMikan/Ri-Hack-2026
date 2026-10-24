using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RematchAndGhosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ghost_offer_declined",
                table: "queue_entries",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ghost_offer_match_id",
                table: "queue_entries",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ghost_offer_seat",
                table: "queue_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rematch_match_id",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "rematch_of_match_id",
                table: "matches",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rematch_requested_at",
                table: "matches",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rematch_requested_by_seat",
                table: "matches",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rematch_status",
                table: "matches",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ghost_source_match_id",
                table: "match_players",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ghost_offers",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ghost_offers", x => new { x.user_id, x.source_match_id });
                    table.ForeignKey(
                        name: "fk_ghost_offers_matches_source_match_id",
                        column: x => x.source_match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_ghost_offers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_queue_entries_ghost_offer_match_id",
                table: "queue_entries",
                column: "ghost_offer_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_rematch_match_id",
                table: "matches",
                column: "rematch_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_rematch_of_match_id",
                table: "matches",
                column: "rematch_of_match_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_matches_rematch_status",
                table: "matches",
                sql: "rematch_status in ('none', 'pending', 'accepted', 'declined', 'expired')");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_ghost_source_match_id",
                table: "match_players",
                column: "ghost_source_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_ghost_offers_source_match_id",
                table: "ghost_offers",
                column: "source_match_id");

            migrationBuilder.AddForeignKey(
                name: "fk_match_players_matches_ghost_source_match_id",
                table: "match_players",
                column: "ghost_source_match_id",
                principalTable: "matches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_matches_rematch_match_id",
                table: "matches",
                column: "rematch_match_id",
                principalTable: "matches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_matches_matches_rematch_of_match_id",
                table: "matches",
                column: "rematch_of_match_id",
                principalTable: "matches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_queue_entries_matches_ghost_offer_match_id",
                table: "queue_entries",
                column: "ghost_offer_match_id",
                principalTable: "matches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_match_players_matches_ghost_source_match_id",
                table: "match_players");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_matches_rematch_match_id",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "fk_matches_matches_rematch_of_match_id",
                table: "matches");

            migrationBuilder.DropForeignKey(
                name: "fk_queue_entries_matches_ghost_offer_match_id",
                table: "queue_entries");

            migrationBuilder.DropTable(
                name: "ghost_offers");

            migrationBuilder.DropIndex(
                name: "ix_queue_entries_ghost_offer_match_id",
                table: "queue_entries");

            migrationBuilder.DropIndex(
                name: "ix_matches_rematch_match_id",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_matches_rematch_of_match_id",
                table: "matches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_matches_rematch_status",
                table: "matches");

            migrationBuilder.DropIndex(
                name: "ix_match_players_ghost_source_match_id",
                table: "match_players");

            migrationBuilder.DropColumn(
                name: "ghost_offer_declined",
                table: "queue_entries");

            migrationBuilder.DropColumn(
                name: "ghost_offer_match_id",
                table: "queue_entries");

            migrationBuilder.DropColumn(
                name: "ghost_offer_seat",
                table: "queue_entries");

            migrationBuilder.DropColumn(
                name: "rematch_match_id",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "rematch_of_match_id",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "rematch_requested_at",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "rematch_requested_by_seat",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "rematch_status",
                table: "matches");

            migrationBuilder.DropColumn(
                name: "ghost_source_match_id",
                table: "match_players");
        }
    }
}
