using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Seasons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "equipped_title_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "season_id",
                table: "reward_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reverted_at",
                table: "answers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "seasons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_seasons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "titles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_titles", x => x.id);
                    table.CheckConstraint("ck_titles_kind", "kind in ('season', 'knowledge', 'weekly')");
                });

            migrationBuilder.CreateTable(
                name: "user_achievements",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    earned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_achievements", x => new { x.user_id, x.key });
                    table.ForeignKey(
                        name: "fk_user_achievements_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weekly_results",
                columns: table => new
                {
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_weekly_results", x => new { x.week_start, x.user_id });
                    table.ForeignKey(
                        name: "fk_weekly_results_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_seasons",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    season_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sp = table.Column<int>(type: "integer", nullable: false),
                    best_tier = table.Column<string>(type: "text", nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_seasons", x => new { x.user_id, x.season_id });
                    table.CheckConstraint("ck_user_seasons_best_tier", "best_tier in ('bronze', 'silver', 'gold', 'platinum', 'diamond', 'master')");
                    table.ForeignKey(
                        name: "fk_user_seasons_seasons_season_id",
                        column: x => x.season_id,
                        principalTable: "seasons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_seasons_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_titles",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title_id = table.Column<Guid>(type: "uuid", nullable: false),
                    earned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_titles", x => new { x.user_id, x.title_id });
                    table.ForeignKey(
                        name: "fk_user_titles_titles_title_id",
                        column: x => x.title_id,
                        principalTable: "titles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_user_titles_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_equipped_title_id",
                table: "users",
                column: "equipped_title_id");

            migrationBuilder.CreateIndex(
                name: "ix_reward_events_season_id_user_id",
                table: "reward_events",
                columns: new[] { "season_id", "user_id" },
                filter: "sp <> 0");

            migrationBuilder.CreateIndex(
                name: "ix_seasons_number",
                table: "seasons",
                column: "number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_titles_key",
                table: "titles",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_seasons_season_id_sp",
                table: "user_seasons",
                columns: new[] { "season_id", "sp" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_user_titles_title_id",
                table: "user_titles",
                column: "title_id");

            migrationBuilder.CreateIndex(
                name: "ix_weekly_results_user_id",
                table: "weekly_results",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_reward_events_seasons_season_id",
                table: "reward_events",
                column: "season_id",
                principalTable: "seasons",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_users_titles_equipped_title_id",
                table: "users",
                column: "equipped_title_id",
                principalTable: "titles",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_reward_events_seasons_season_id",
                table: "reward_events");

            migrationBuilder.DropForeignKey(
                name: "fk_users_titles_equipped_title_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "user_achievements");

            migrationBuilder.DropTable(
                name: "user_seasons");

            migrationBuilder.DropTable(
                name: "user_titles");

            migrationBuilder.DropTable(
                name: "weekly_results");

            migrationBuilder.DropTable(
                name: "seasons");

            migrationBuilder.DropTable(
                name: "titles");

            migrationBuilder.DropIndex(
                name: "ix_users_equipped_title_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_reward_events_season_id_user_id",
                table: "reward_events");

            migrationBuilder.DropColumn(
                name: "equipped_title_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "season_id",
                table: "reward_events");

            migrationBuilder.DropColumn(
                name: "reverted_at",
                table: "answers");
        }
    }
}
