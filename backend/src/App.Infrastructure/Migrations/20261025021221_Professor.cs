using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Professor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "professor_chats",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professor_chats", x => x.id);
                    table.ForeignKey(
                        name: "fk_professor_chats_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "professor_messages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    topic = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    scope = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professor_messages", x => x.id);
                    table.CheckConstraint("ck_professor_messages_role", "role in ('user', 'professor')");
                    table.CheckConstraint("ck_professor_messages_scope", "scope is null or scope in ('specific', 'general')");
                    table.ForeignKey(
                        name: "fk_professor_messages_professor_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "professor_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "professor_lessons",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    summary = table.Column<string>(type: "text", nullable: false),
                    cards = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professor_lessons", x => x.id);
                    table.ForeignKey(
                        name: "fk_professor_lessons_professor_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "professor_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_professor_lessons_professor_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "professor_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_professor_lessons_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "professor_quizzes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    chat_id = table.Column<Guid>(type: "uuid", nullable: false),
                    message_id = table.Column<Guid>(type: "uuid", nullable: false),
                    topic = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    questions = table.Column<string>(type: "jsonb", nullable: false),
                    answers = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_professor_quizzes", x => x.id);
                    table.ForeignKey(
                        name: "fk_professor_quizzes_professor_chats_chat_id",
                        column: x => x.chat_id,
                        principalTable: "professor_chats",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_professor_quizzes_professor_messages_message_id",
                        column: x => x.message_id,
                        principalTable: "professor_messages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_professor_quizzes_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_professor_chats_user_id_updated_at",
                table: "professor_chats",
                columns: new[] { "user_id", "updated_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_professor_lessons_chat_id",
                table: "professor_lessons",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_professor_lessons_message_id",
                table: "professor_lessons",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professor_lessons_user_id",
                table: "professor_lessons",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_professor_messages_chat_id_created_at",
                table: "professor_messages",
                columns: new[] { "chat_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_professor_quizzes_chat_id",
                table: "professor_quizzes",
                column: "chat_id");

            migrationBuilder.CreateIndex(
                name: "ix_professor_quizzes_message_id",
                table: "professor_quizzes",
                column: "message_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_professor_quizzes_user_id",
                table: "professor_quizzes",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "professor_lessons");

            migrationBuilder.DropTable(
                name: "professor_quizzes");

            migrationBuilder.DropTable(
                name: "professor_messages");

            migrationBuilder.DropTable(
                name: "professor_chats");
        }
    }
}
