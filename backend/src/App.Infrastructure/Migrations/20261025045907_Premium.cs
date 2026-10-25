using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace App.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Premium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_reward_events_type",
                table: "reward_events");

            migrationBuilder.AddColumn<bool>(
                name: "premium_auto_renew",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "premium_card_last4",
                table: "users",
                type: "character varying(4)",
                maxLength: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "premium_interval",
                table: "users",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "premium_until",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_users_premium_interval",
                table: "users",
                sql: "premium_interval is null or premium_interval in ('monthly', 'yearly')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reward_events_type",
                table: "reward_events",
                sql: "type in ('match_win', 'match_loss', 'match_draw', 'rating_upset', 'lesson_passed', 'concept_patched', 'daily_challenge', 'daily_task', 'daily_task_bonus', 'streak_milestone', 'weekly_xp_goal', 'level_up', 'friend_match', 'answer_xp', 'match_xp', 'shop_purchase', 'streak_freeze', 'season_reset', 'season_drop', 'premium_daily')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_premium_interval",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reward_events_type",
                table: "reward_events");

            migrationBuilder.DropColumn(
                name: "premium_auto_renew",
                table: "users");

            migrationBuilder.DropColumn(
                name: "premium_card_last4",
                table: "users");

            migrationBuilder.DropColumn(
                name: "premium_interval",
                table: "users");

            migrationBuilder.DropColumn(
                name: "premium_until",
                table: "users");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reward_events_type",
                table: "reward_events",
                sql: "type in ('match_win', 'match_loss', 'match_draw', 'rating_upset', 'lesson_passed', 'concept_patched', 'daily_challenge', 'daily_task', 'daily_task_bonus', 'streak_milestone', 'weekly_xp_goal', 'level_up', 'friend_match', 'answer_xp', 'match_xp', 'shop_purchase', 'streak_freeze', 'season_reset', 'season_drop')");
        }
    }
}
