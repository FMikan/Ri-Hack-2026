using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewAndLessons : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "lesson_id",
                table: "answers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "concept_lesson_cards",
                columns: table => new
                {
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    band = table.Column<int>(type: "integer", nullable: false),
                    cards = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concept_lesson_cards", x => new { x.concept_id, x.band });
                    table.ForeignKey(
                        name: "fk_concept_lesson_cards_concepts_concept_id",
                        column: x => x.concept_id,
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    concept_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    cards = table.Column<string>(type: "jsonb", nullable: false),
                    cards_source = table.Column<string>(type: "text", nullable: false),
                    check_question_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    check_results = table.Column<string>(type: "jsonb", nullable: true),
                    attempt = table.Column<int>(type: "integer", nullable: false),
                    last_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_for_match = table.Column<bool>(type: "boolean", nullable: false),
                    rewarded = table.Column<bool>(type: "boolean", nullable: false),
                    retry_after = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_lessons", x => x.id);
                    table.CheckConstraint("ck_lessons_cards_source", "cards_source in ('ai', 'cache', 'fallback')");
                    table.CheckConstraint("ck_lessons_status", "status in ('generated', 'passed', 'failed')");
                    table.ForeignKey(
                        name: "fk_lessons_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_lessons_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "question_reports",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_question_reports", x => x.id);
                    table.CheckConstraint("ck_question_reports_reason", "length(reason) between 1 and 500");
                    table.ForeignKey(
                        name: "fk_question_reports_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_question_reports_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_answers_lesson_id",
                table: "answers",
                column: "lesson_id");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_match_id",
                table: "lessons",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_lessons_user_id_created_at",
                table: "lessons",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_question_reports_question_id_user_id",
                table: "question_reports",
                columns: new[] { "question_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_question_reports_user_id",
                table: "question_reports",
                column: "user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_answers_lessons_lesson_id",
                table: "answers",
                column: "lesson_id",
                principalTable: "lessons",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_answers_lessons_lesson_id",
                table: "answers");

            migrationBuilder.DropTable(
                name: "concept_lesson_cards");

            migrationBuilder.DropTable(
                name: "lessons");

            migrationBuilder.DropTable(
                name: "question_reports");

            migrationBuilder.DropIndex(
                name: "ix_answers_lesson_id",
                table: "answers");

            migrationBuilder.DropColumn(
                name: "lesson_id",
                table: "answers");
        }
    }
}
