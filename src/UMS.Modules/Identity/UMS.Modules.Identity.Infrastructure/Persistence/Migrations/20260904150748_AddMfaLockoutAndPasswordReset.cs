using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Identity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMfaLockoutAndPasswordReset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "locked_out_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "mfa_enrolled_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mfa_enrolled_secret_cipher_text",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "mfa_pending_created_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "mfa_pending_expires_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "mfa_pending_secret_cipher_text",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_reset_consumed_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_reset_created_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "password_reset_expires_at",
                schema: "identity",
                table: "users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "password_reset_token_hash",
                schema: "identity",
                table: "users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "requires_mfa",
                schema: "identity",
                table: "roles",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_users_password_reset_token_hash",
                schema: "identity",
                table: "users",
                column: "password_reset_token_hash");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_users_password_reset_token_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "locked_out_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_enrolled_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_enrolled_secret_cipher_text",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_pending_created_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_pending_expires_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "mfa_pending_secret_cipher_text",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_consumed_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_created_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_expires_at",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "password_reset_token_hash",
                schema: "identity",
                table: "users");

            migrationBuilder.DropColumn(
                name: "requires_mfa",
                schema: "identity",
                table: "roles");
        }
    }
}
