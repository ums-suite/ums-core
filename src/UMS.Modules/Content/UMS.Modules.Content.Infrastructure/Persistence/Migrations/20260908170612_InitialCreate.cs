using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Content.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "content");

            migrationBuilder.CreateTable(
                name: "banners",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    headline = table.Column<string>(type: "text", nullable: false),
                    image_url = table.Column<string>(type: "text", nullable: false),
                    link_url = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expire_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_banners", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "download_resources",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expire_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_download_resources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "events",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    location_label = table.Column<string>(type: "text", nullable: true),
                    audience = table.Column<int>(type: "integer", nullable: false),
                    organization_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "homepage_sections",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    reference_organization_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_homepage_sections", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notices",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    audience = table.Column<int>(type: "integer", nullable: false),
                    organization_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_urgent = table.Column<bool>(type: "boolean", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    publish_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expire_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "content",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
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
                name: "event_translations",
                schema: "content",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    location_label = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_event_translations", x => new { x.event_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_event_translations_events_event_id",
                        column: x => x.event_id,
                        principalSchema: "content",
                        principalTable: "events",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "notice_translations",
                schema: "content",
                columns: table => new
                {
                    notice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notice_translations", x => new { x.notice_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_notice_translations_notices_notice_id",
                        column: x => x.notice_id,
                        principalSchema: "content",
                        principalTable: "notices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_banners_sort_order_created_at",
                schema: "content",
                table: "banners",
                columns: new[] { "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_banners_status_expire_at",
                schema: "content",
                table: "banners",
                columns: new[] { "status", "expire_at" });

            migrationBuilder.CreateIndex(
                name: "ix_banners_status_publish_at",
                schema: "content",
                table: "banners",
                columns: new[] { "status", "publish_at" });

            migrationBuilder.CreateIndex(
                name: "ix_download_resources_category",
                schema: "content",
                table: "download_resources",
                column: "category");

            migrationBuilder.CreateIndex(
                name: "ix_download_resources_status_expire_at",
                schema: "content",
                table: "download_resources",
                columns: new[] { "status", "expire_at" });

            migrationBuilder.CreateIndex(
                name: "ix_download_resources_status_publish_at",
                schema: "content",
                table: "download_resources",
                columns: new[] { "status", "publish_at" });

            migrationBuilder.CreateIndex(
                name: "ux_event_translations_event_language",
                schema: "content",
                table: "event_translations",
                columns: new[] { "event_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_events_audience",
                schema: "content",
                table: "events",
                column: "audience");

            migrationBuilder.CreateIndex(
                name: "ix_events_start_end",
                schema: "content",
                table: "events",
                columns: new[] { "start_at", "end_at" });

            migrationBuilder.CreateIndex(
                name: "ix_homepage_sections_sort_order_created_at",
                schema: "content",
                table: "homepage_sections",
                columns: new[] { "sort_order", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_homepage_sections_section_key",
                schema: "content",
                table: "homepage_sections",
                column: "section_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_notice_translations_notice_language",
                schema: "content",
                table: "notice_translations",
                columns: new[] { "notice_id", "language_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notices_audience_status",
                schema: "content",
                table: "notices",
                columns: new[] { "audience", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_notices_status_expire_at",
                schema: "content",
                table: "notices",
                columns: new[] { "status", "expire_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notices_status_publish_at",
                schema: "content",
                table: "notices",
                columns: new[] { "status", "publish_at" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "content",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "content",
                table: "outbox_messages",
                column: "processed_at");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banners",
                schema: "content");

            migrationBuilder.DropTable(
                name: "download_resources",
                schema: "content");

            migrationBuilder.DropTable(
                name: "event_translations",
                schema: "content");

            migrationBuilder.DropTable(
                name: "homepage_sections",
                schema: "content");

            migrationBuilder.DropTable(
                name: "notice_translations",
                schema: "content");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "content");

            migrationBuilder.DropTable(
                name: "events",
                schema: "content");

            migrationBuilder.DropTable(
                name: "notices",
                schema: "content");
        }
    }
}
