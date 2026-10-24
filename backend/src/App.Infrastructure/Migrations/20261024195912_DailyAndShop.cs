using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DailyAndShop : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "daily_challenges",
                columns: table => new
                {
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_challenges", x => x.day);
                    table.ForeignKey(
                        name: "fk_daily_challenges_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "shop_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    price = table.Column<int>(type: "integer", nullable: false),
                    rarity = table.Column<string>(type: "text", nullable: false),
                    asset_key = table.Column<string>(type: "text", nullable: false),
                    season_drop_tier = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shop_items", x => x.id);
                    table.CheckConstraint("ck_shop_items_kind", "kind in ('avatar_frame', 'banner', 'board_theme', 'name_color', 'card_background', 'streak_freeze')");
                    table.CheckConstraint("ck_shop_items_price", "price > 0");
                });

            migrationBuilder.CreateTable(
                name: "user_daily_tasks",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    task_key = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    target = table.Column<int>(type: "integer", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_daily_tasks", x => new { x.user_id, x.day, x.task_key });
                    table.ForeignKey(
                        name: "fk_user_daily_tasks_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_user_daily_tasks_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "daily_challenge_attempts",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: true),
                    fraction = table.Column<double>(type: "double precision", nullable: true),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_daily_challenge_attempts", x => new { x.user_id, x.day });
                    table.ForeignKey(
                        name: "fk_daily_challenge_attempts_daily_challenges_day",
                        column: x => x.day,
                        principalTable: "daily_challenges",
                        principalColumn: "day",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_daily_challenge_attempts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_items",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    acquired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_items", x => new { x.user_id, x.item_id });
                    table.ForeignKey(
                        name: "fk_user_items_shop_items_item_id",
                        column: x => x.item_id,
                        principalTable: "shop_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_daily_challenge_attempts_day_is_correct_elapsed_ms",
                table: "daily_challenge_attempts",
                columns: new[] { "day", "is_correct", "elapsed_ms" },
                descending: new[] { false, true, false });

            migrationBuilder.CreateIndex(
                name: "ix_daily_challenges_question_id",
                table: "daily_challenges",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_shop_items_key",
                table: "shop_items",
                column: "key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_daily_tasks_topic_id",
                table: "user_daily_tasks",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_items_item_id",
                table: "user_items",
                column: "item_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "daily_challenge_attempts");

            migrationBuilder.DropTable(
                name: "user_daily_tasks");

            migrationBuilder.DropTable(
                name: "user_items");

            migrationBuilder.DropTable(
                name: "daily_challenges");

            migrationBuilder.DropTable(
                name: "shop_items");
        }
    }
}
