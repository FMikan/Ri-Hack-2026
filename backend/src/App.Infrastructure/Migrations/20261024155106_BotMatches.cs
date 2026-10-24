using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BotMatches : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "current_match_id",
                table: "users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "concept_mastery",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mastery = table.Column<double>(type: "double precision", nullable: false),
                    answer_count = table.Column<int>(type: "integer", nullable: false),
                    is_graduated = table.Column<bool>(type: "boolean", nullable: false),
                    review_step = table.Column<int>(type: "integer", nullable: true),
                    review_due_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    consecutive_review_successes = table.Column<int>(type: "integer", nullable: false),
                    patched_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_concept_mastery", x => new { x.user_id, x.concept_id });
                    table.CheckConstraint("ck_concept_mastery_mastery", "mastery between 0 and 100");
                    table.CheckConstraint("ck_concept_mastery_review_step", "review_step between 1 and 5");
                    table.ForeignKey(
                        name: "fk_concept_mastery_concepts_concept_id",
                        column: x => x.concept_id,
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_concept_mastery_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_concept_mastery_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "matches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    phase = table.Column<string>(type: "text", nullable: false),
                    phase_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: true),
                    timed = table.Column<bool>(type: "boolean", nullable: false),
                    band_min = table.Column<int>(type: "integer", nullable: false),
                    band_max = table.Column<int>(type: "integer", nullable: false),
                    current_index = table.Column<int>(type: "integer", nullable: false),
                    question_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rng_seed = table.Column<int>(type: "integer", nullable: false),
                    winner_seat = table.Column<int>(type: "integer", nullable: true),
                    result_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_matches", x => x.id);
                    table.CheckConstraint("ck_matches_kind", "kind in ('ranked', 'ghost', 'bot', 'friend', 'practice', 'calibration')");
                    table.CheckConstraint("ck_matches_phase", "phase in ('veto_coin', 'veto_ban', 'veto_pick', 'lobby', 'question', 'interstitial', 'done')");
                    table.CheckConstraint("ck_matches_result_reason", "result_reason in ('score', 'tiebreak_correct', 'tiebreak_time', 'draw', 'resign', 'disconnect', 'aborted')");
                    table.ForeignKey(
                        name: "fk_matches_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "questions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    seed_key = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    remember = table.Column<string>(type: "text", nullable: false),
                    explanation = table.Column<string>(type: "jsonb", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_questions", x => x.id);
                    table.CheckConstraint("ck_questions_difficulty", "difficulty between 1 and 4");
                    table.CheckConstraint("ck_questions_status", "status in ('active', 'reported', 'quarantine', 'retired')");
                    table.CheckConstraint("ck_questions_type", "type in ('true_false', 'multiple_choice', 'cloze', 'odd_one_out', 'output', 'match_pairs', 'order', 'find_bug')");
                    table.ForeignKey(
                        name: "fk_questions_concepts_concept_id",
                        column: x => x.concept_id,
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_questions_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "match_answers",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat = table.Column<int>(type: "integer", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    fraction = table.Column<double>(type: "double precision", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    timed_out = table.Column<bool>(type: "boolean", nullable: false),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_answers", x => new { x.match_id, x.seat, x.index });
                    table.CheckConstraint("ck_match_answers_label", "label in ('brilliant', 'good', 'miss', 'blunder')");
                    table.ForeignKey(
                        name: "fk_match_answers_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_players",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    seat = table.Column<int>(type: "integer", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bot_persona = table.Column<string>(type: "text", nullable: true),
                    display_rating = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<int>(type: "integer", nullable: false),
                    correct_count = table.Column<int>(type: "integer", nullable: false),
                    time_on_correct_ms = table.Column<int>(type: "integer", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_index = table.Column<int>(type: "integer", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    resigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sp_earned = table.Column<int>(type: "integer", nullable: false),
                    coins_earned = table.Column<int>(type: "integer", nullable: false),
                    xp_earned = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_players", x => new { x.match_id, x.seat });
                    table.CheckConstraint("ck_match_players_bot_persona", "bot_persona in ('vibe_coder', 'junior', 'mid', 'senior')");
                    table.CheckConstraint("ck_match_players_kind", "kind in ('human', 'bot', 'ghost')");
                    table.CheckConstraint("ck_match_players_seat", "seat in (0, 1)");
                    table.ForeignKey(
                        name: "fk_match_players_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_players_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic_id = table.Column<Guid>(type: "uuid", nullable: false),
                    question_type = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: true),
                    is_correct = table.Column<bool>(type: "boolean", nullable: false),
                    fraction = table.Column<double>(type: "double precision", nullable: false),
                    points = table.Column<int>(type: "integer", nullable: false),
                    elapsed_ms = table.Column<int>(type: "integer", nullable: false),
                    time_limit_ms = table.Column<int>(type: "integer", nullable: true),
                    label = table.Column<string>(type: "text", nullable: true),
                    mastery_before = table.Column<double>(type: "double precision", nullable: false),
                    mastery_after = table.Column<double>(type: "double precision", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_answers", x => x.id);
                    table.CheckConstraint("ck_answers_label", "label in ('brilliant', 'good', 'miss', 'blunder')");
                    table.CheckConstraint("ck_answers_question_type", "question_type in ('true_false', 'multiple_choice', 'cloze', 'odd_one_out', 'output', 'match_pairs', 'order', 'find_bug')");
                    table.CheckConstraint("ck_answers_source", "source in ('ranked', 'ghost', 'bot', 'friend', 'practice', 'calibration', 'lesson_check', 'daily_challenge')");
                    table.ForeignKey(
                        name: "fk_answers_concepts_concept_id",
                        column: x => x.concept_id,
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answers_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_answers_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answers_topics_topic_id",
                        column: x => x.topic_id,
                        principalTable: "topics",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_answers_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "match_questions",
                columns: table => new
                {
                    match_id = table.Column<Guid>(type: "uuid", nullable: false),
                    index = table.Column<int>(type: "integer", nullable: false),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    concept_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<string>(type: "text", nullable: false),
                    type = table.Column<string>(type: "text", nullable: false),
                    difficulty = table.Column<int>(type: "integer", nullable: false),
                    base_points = table.Column<int>(type: "integer", nullable: false),
                    time_limit_ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_match_questions", x => new { x.match_id, x.index });
                    table.CheckConstraint("ck_match_questions_index", "index between 0 and 9");
                    table.CheckConstraint("ck_match_questions_slot", "slot in ('warmup', 'targeted', 'core', 'review')");
                    table.CheckConstraint("ck_match_questions_type", "type in ('true_false', 'multiple_choice', 'cloze', 'odd_one_out', 'output', 'match_pairs', 'order', 'find_bug')");
                    table.ForeignKey(
                        name: "fk_match_questions_concepts_concept_id",
                        column: x => x.concept_id,
                        principalTable: "concepts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_match_questions_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_match_questions_questions_question_id",
                        column: x => x.question_id,
                        principalTable: "questions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_users_current_match_id",
                table: "users",
                column: "current_match_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_concept_id",
                table: "answers",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_match_id",
                table: "answers",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_question_id",
                table: "answers",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_topic_id",
                table: "answers",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_answers_user_id_concept_id_created_at",
                table: "answers",
                columns: new[] { "user_id", "concept_id", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_answers_user_id_created_at",
                table: "answers",
                columns: new[] { "user_id", "created_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_concept_mastery_concept_id",
                table: "concept_mastery",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_concept_mastery_topic_id_user_id",
                table: "concept_mastery",
                columns: new[] { "topic_id", "user_id" },
                filter: "is_graduated");

            migrationBuilder.CreateIndex(
                name: "ix_concept_mastery_user_id_review_due_at",
                table: "concept_mastery",
                columns: new[] { "user_id", "review_due_at" },
                filter: "review_due_at is not null");

            migrationBuilder.CreateIndex(
                name: "ix_match_players_user_id_match_id",
                table: "match_players",
                columns: new[] { "user_id", "match_id" });

            migrationBuilder.CreateIndex(
                name: "ix_match_questions_concept_id",
                table: "match_questions",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_match_questions_question_id",
                table: "match_questions",
                column: "question_id");

            migrationBuilder.CreateIndex(
                name: "ix_matches_finished_at",
                table: "matches",
                column: "finished_at",
                filter: "kind in ('ranked', 'ghost')");

            migrationBuilder.CreateIndex(
                name: "ix_matches_phase_deadline",
                table: "matches",
                column: "phase_deadline",
                filter: "phase <> 'done'");

            migrationBuilder.CreateIndex(
                name: "ix_matches_topic_id",
                table: "matches",
                column: "topic_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_concept_id",
                table: "questions",
                column: "concept_id");

            migrationBuilder.CreateIndex(
                name: "ix_questions_seed_key",
                table: "questions",
                column: "seed_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_questions_topic_id",
                table: "questions",
                column: "topic_id",
                filter: "status = 'active'");

            migrationBuilder.AddForeignKey(
                name: "fk_users_matches_current_match_id",
                table: "users",
                column: "current_match_id",
                principalTable: "matches",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_matches_current_match_id",
                table: "users");

            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "concept_mastery");

            migrationBuilder.DropTable(
                name: "match_answers");

            migrationBuilder.DropTable(
                name: "match_players");

            migrationBuilder.DropTable(
                name: "match_questions");

            migrationBuilder.DropTable(
                name: "matches");

            migrationBuilder.DropTable(
                name: "questions");

            migrationBuilder.DropIndex(
                name: "ix_users_current_match_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "current_match_id",
                table: "users");
        }
    }
}
