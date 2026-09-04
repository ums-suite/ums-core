using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Notifications.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "notifications");

            migrationBuilder.CreateTable(
                name: "channel_suppressions",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    suppressed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_channel_suppressions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_requests",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_entity_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    language_override = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_outbox_messages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "recipient_notification_preferences",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    opted_out = table.Column<bool>(type: "boolean", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recipient_notification_preferences", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "templates",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_delivery_attempts",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    notification_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    channel = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    in_flight_since = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    delivered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provider_message_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    dead_letter_reason = table.Column<int>(type: "integer", nullable: true),
                    rendered_subject = table.Column<string>(type: "text", nullable: true),
                    rendered_body = table.Column<string>(type: "text", nullable: true),
                    rendered_deep_link = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_delivery_attempts", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_delivery_attempts_notification_requests_notifi~",
                        column: x => x.notification_request_id,
                        principalSchema: "notifications",
                        principalTable: "notification_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_translations",
                schema: "notifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    subject = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    body = table.Column<string>(type: "text", nullable: false),
                    push_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    deep_link = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_template_translations", x => x.id);
                    table.ForeignKey(
                        name: "FK_template_translations_templates_template_id",
                        column: x => x.template_id,
                        principalSchema: "notifications",
                        principalTable: "templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_channel_suppressions_recipient_channel",
                schema: "notifications",
                table: "channel_suppressions",
                columns: new[] { "recipient_id", "channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_dispatch",
                schema: "notifications",
                table: "notification_delivery_attempts",
                columns: new[] { "channel", "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_provider_message",
                schema: "notifications",
                table: "notification_delivery_attempts",
                columns: new[] { "channel", "provider_message_id" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_request",
                schema: "notifications",
                table: "notification_delivery_attempts",
                column: "notification_request_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_delivery_attempts_status",
                schema: "notifications",
                table: "notification_delivery_attempts",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_notification_requests_recipient",
                schema: "notifications",
                table: "notification_requests",
                column: "recipient_id");

            migrationBuilder.CreateIndex(
                name: "ux_notification_requests_dedupe",
                schema: "notifications",
                table: "notification_requests",
                columns: new[] { "source_module", "event_type", "source_entity_id", "recipient_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notifications_outbox_messages_processed_at",
                schema: "notifications",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ux_recipient_notification_preferences_recipient_category",
                schema: "notifications",
                table: "recipient_notification_preferences",
                columns: new[] { "recipient_id", "category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_template_translations_template_language",
                schema: "notifications",
                table: "template_translations",
                columns: new[] { "template_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_templates_event_type_channel",
                schema: "notifications",
                table: "templates",
                columns: new[] { "event_type", "channel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "channel_suppressions",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_delivery_attempts",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "recipient_notification_preferences",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "template_translations",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "notification_requests",
                schema: "notifications");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "notifications");
        }
    }
}
