using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Hostel.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hostel");

            migrationBuilder.CreateTable(
                name: "allocation_review_flags",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_event_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocation_review_flags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "allocations",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    bed_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostel_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    fee_grace_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    check_out_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    refund_requested = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fee_paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    checked_out_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_allocations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "application_windows",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    eligible_program_ids = table.Column<string>(type: "jsonb", nullable: false),
                    eligible_years = table.Column<string>(type: "jsonb", nullable: false),
                    eligibility_rules = table.Column<string>(type: "jsonb", nullable: false),
                    rules_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_windows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "beds",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_beds", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "complaints",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    resolution_note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_complaints", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hostel_applications",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_window_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    year_of_study = table.Column<int>(type: "integer", nullable: false),
                    has_financial_need = table.Column<bool>(type: "boolean", nullable: false),
                    home_district_distance_km = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    eligibility_score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    is_eligible = table.Column<bool>(type: "boolean", nullable: true),
                    rank_position = table.Column<int>(type: "integer", nullable: true),
                    decision_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ranked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hostel_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hostels",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hostels", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "hostel",
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
                name: "processed_inbound_events",
                schema: "hostel",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_inbound_events", x => new { x.event_id, x.source });
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "hostel",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hostel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    room_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "hostel_application_preferences",
                schema: "hostel",
                columns: table => new
                {
                    hostel_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    hostel_id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_room_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_hostel_application_preferences", x => new { x.hostel_application_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_hostel_application_preferences_hostel_applications_hostel_a~",
                        column: x => x.hostel_application_id,
                        principalSchema: "hostel",
                        principalTable: "hostel_applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocation_review_flags_allocation_id",
                schema: "hostel",
                table: "allocation_review_flags",
                column: "allocation_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_invoice_id",
                schema: "hostel",
                table: "allocations",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocations_status_grace_deadline",
                schema: "hostel",
                table: "allocations",
                columns: new[] { "status", "fee_grace_deadline" });

            migrationBuilder.CreateIndex(
                name: "ux_allocations_bed_nonterminal",
                schema: "hostel",
                table: "allocations",
                column: "bed_id",
                unique: true,
                filter: "status IN ('Pending', 'FeePaid', 'Active')");

            migrationBuilder.CreateIndex(
                name: "ux_allocations_student_nonterminal",
                schema: "hostel",
                table: "allocations",
                column: "student_id",
                unique: true,
                filter: "status IN ('Pending', 'FeePaid', 'Active')");

            migrationBuilder.CreateIndex(
                name: "ix_beds_room_id",
                schema: "hostel",
                table: "beds",
                column: "room_id");

            migrationBuilder.CreateIndex(
                name: "ux_beds_room_label",
                schema: "hostel",
                table: "beds",
                columns: new[] { "room_id", "label" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_buildings_hostel_id",
                schema: "hostel",
                table: "buildings",
                column: "hostel_id");

            migrationBuilder.CreateIndex(
                name: "ix_complaints_dedupe_window",
                schema: "hostel",
                table: "complaints",
                columns: new[] { "student_id", "allocation_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_complaints_student_id",
                schema: "hostel",
                table: "complaints",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_complaints_idempotency_key",
                schema: "hostel",
                table: "complaints",
                column: "idempotency_key",
                unique: true,
                filter: "idempotency_key IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_hostel_applications_window_status",
                schema: "hostel",
                table: "hostel_applications",
                columns: new[] { "application_window_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_hostel_applications_student_window_open",
                schema: "hostel",
                table: "hostel_applications",
                columns: new[] { "student_id", "application_window_id" },
                unique: true,
                filter: "status IN ('Draft', 'Submitted', 'Ranked', 'Waitlisted', 'Approved')");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "hostel",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "hostel",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_building_id",
                schema: "hostel",
                table: "rooms",
                column: "building_id");

            migrationBuilder.CreateIndex(
                name: "ix_rooms_hostel_id_type",
                schema: "hostel",
                table: "rooms",
                columns: new[] { "hostel_id", "type" });

            migrationBuilder.CreateIndex(
                name: "ux_rooms_building_room_number",
                schema: "hostel",
                table: "rooms",
                columns: new[] { "building_id", "room_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_review_flags",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "allocations",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "application_windows",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "beds",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "buildings",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "complaints",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "hostel_application_preferences",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "hostels",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "hostel");

            migrationBuilder.DropTable(
                name: "hostel_applications",
                schema: "hostel");
        }
    }
}
