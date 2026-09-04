using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Documents.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "bulk_generation_job_items",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    render_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    generated_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_generation_job_items", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "bulk_generation_jobs",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    total_items = table.Column<int>(type: "integer", nullable: false),
                    completed_count = table.Column<int>(type: "integer", nullable: false),
                    dead_lettered_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bulk_generation_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_templates",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    layout_asset_key = table.Column<string>(type: "text", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "generated_documents",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "text", nullable: false),
                    source_reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_version = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: true),
                    checksum = table.Column<string>(type: "text", nullable: true),
                    mime_type = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    digital_verification_id = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_reason = table.Column<string>(type: "text", nullable: true),
                    superseded_by_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bulk_generation_job_id = table.Column<Guid>(type: "uuid", nullable: true),
                    render_data_json = table.Column<string>(type: "jsonb", nullable: false),
                    language_code = table.Column<string>(type: "text", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_generated_documents", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "documents",
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
                name: "uploaded_artifacts",
                schema: "documents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    artifact_type = table.Column<string>(type: "text", nullable: false),
                    mime_type = table.Column<string>(type: "text", nullable: false),
                    storage_key = table.Column<string>(type: "text", nullable: false),
                    checksum = table.Column<string>(type: "text", nullable: true),
                    size_bytes = table.Column<long>(type: "bigint", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ready_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_uploaded_artifacts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_template_translations",
                schema: "documents",
                columns: table => new
                {
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    labels_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_template_translations", x => new { x.template_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_document_template_translations_document_templates_template_~",
                        column: x => x.template_id,
                        principalSchema: "documents",
                        principalTable: "document_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_bulk_generation_job_items_job_status",
                schema: "documents",
                table: "bulk_generation_job_items",
                columns: new[] { "job_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_bulk_generation_jobs_status",
                schema: "documents",
                table: "bulk_generation_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_document_templates_type_version",
                schema: "documents",
                table: "document_templates",
                columns: new[] { "document_type", "version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_generated_documents_bulk_job_id",
                schema: "documents",
                table: "generated_documents",
                column: "bulk_generation_job_id");

            migrationBuilder.CreateIndex(
                name: "ix_generated_documents_status_created_at",
                schema: "documents",
                table: "generated_documents",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ux_generated_documents_natural_key",
                schema: "documents",
                table: "generated_documents",
                columns: new[] { "owner_id", "document_type", "source_reference_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_generated_documents_verification_id",
                schema: "documents",
                table: "generated_documents",
                column: "digital_verification_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_documents_outbox_messages_event_type_processed_at",
                schema: "documents",
                table: "outbox_messages",
                columns: new[] { "event_type", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_uploaded_artifacts_owner_type",
                schema: "documents",
                table: "uploaded_artifacts",
                columns: new[] { "owner_id", "artifact_type" });

            migrationBuilder.CreateIndex(
                name: "ux_uploaded_artifacts_storage_key",
                schema: "documents",
                table: "uploaded_artifacts",
                column: "storage_key",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "bulk_generation_job_items",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "bulk_generation_jobs",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_template_translations",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "generated_documents",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "uploaded_artifacts",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_templates",
                schema: "documents");
        }
    }
}
