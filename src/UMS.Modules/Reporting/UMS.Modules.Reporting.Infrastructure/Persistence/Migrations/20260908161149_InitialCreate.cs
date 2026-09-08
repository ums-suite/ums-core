using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Reporting.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "reporting");

            migrationBuilder.CreateTable(
                name: "dashboard_metrics",
                schema: "reporting",
                columns: table => new
                {
                    metric_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: true),
                    data_as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_refresh_attempted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_refresh_succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    last_refresh_error = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_dashboard_metrics", x => x.metric_key);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "reporting",
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
                name: "regulatory_report_definitions",
                schema: "reporting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    filters_json = table.Column<string>(type: "jsonb", nullable: false),
                    source_query_references_json = table.Column<string>(type: "jsonb", nullable: false),
                    supported_formats = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_report_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regulatory_report_runs",
                schema: "reporting",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_snapshot_json = table.Column<string>(type: "jsonb", nullable: false),
                    parameters_json = table.Column<string>(type: "jsonb", nullable: false),
                    parameters_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    format = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    as_of = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    result_csv_content = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_report_runs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "regulatory_report_definition_fields",
                schema: "reporting",
                columns: table => new
                {
                    row_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    field_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    definition_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_regulatory_report_definition_fields", x => x.row_id);
                    table.ForeignKey(
                        name: "FK_regulatory_report_definition_fields_regulatory_report_defin~",
                        column: x => x.definition_id,
                        principalSchema: "reporting",
                        principalTable: "regulatory_report_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "reporting",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "reporting",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_report_definition_fields_definition_id",
                schema: "reporting",
                table: "regulatory_report_definition_fields",
                column: "definition_id");

            migrationBuilder.CreateIndex(
                name: "ux_regulatory_report_definitions_name",
                schema: "reporting",
                table: "regulatory_report_definitions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_report_runs_parameters_hash_status",
                schema: "reporting",
                table: "regulatory_report_runs",
                columns: new[] { "parameters_hash", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_regulatory_report_runs_status",
                schema: "reporting",
                table: "regulatory_report_runs",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_metrics",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "regulatory_report_definition_fields",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "regulatory_report_runs",
                schema: "reporting");

            migrationBuilder.DropTable(
                name: "regulatory_report_definitions",
                schema: "reporting");
        }
    }
}
