using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Social : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_grandmaster",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "friend_challenges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(6)", maxLength: 6, nullable: false),
                    host_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    match_id = table.Column<Guid>(type: "uuid", nullable: true),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friend_challenges", x => x.id);
                    table.ForeignKey(
                        name: "fk_friend_challenges_matches_match_id",
                        column: x => x.match_id,
                        principalTable: "matches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_friend_challenges_users_guest_user_id",
                        column: x => x.guest_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_friend_challenges_users_host_user_id",
                        column: x => x.host_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friend_challenges_users_target_user_id",
                        column: x => x.target_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "friendships",
                columns: table => new
                {
                    user_low = table.Column<Guid>(type: "uuid", nullable: false),
                    user_high = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_friendships", x => new { x.user_low, x.user_high });
                    table.CheckConstraint("ck_friendships_order", "user_low < user_high");
                    table.CheckConstraint("ck_friendships_status", "status in ('pending', 'accepted')");
                    table.ForeignKey(
                        name: "fk_friendships_users_requested_by",
                        column: x => x.requested_by,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_users_user_high",
                        column: x => x.user_high,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_friendships_users_user_low",
                        column: x => x.user_low,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_friend_challenges_code",
                table: "friend_challenges",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_friend_challenges_guest_user_id",
                table: "friend_challenges",
                column: "guest_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_challenges_host_user_id",
                table: "friend_challenges",
                column: "host_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_challenges_match_id",
                table: "friend_challenges",
                column: "match_id");

            migrationBuilder.CreateIndex(
                name: "ix_friend_challenges_target_user_id",
                table: "friend_challenges",
                column: "target_user_id",
                filter: "match_id is null");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_requested_by",
                table: "friendships",
                column: "requested_by");

            migrationBuilder.CreateIndex(
                name: "ix_friendships_user_high_status",
                table: "friendships",
                columns: new[] { "user_high", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "friend_challenges");

            migrationBuilder.DropTable(
                name: "friendships");

            migrationBuilder.DropColumn(
                name: "is_grandmaster",
                table: "users");
        }
    }
}
