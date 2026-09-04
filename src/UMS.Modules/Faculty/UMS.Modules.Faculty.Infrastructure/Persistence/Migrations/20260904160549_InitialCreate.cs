using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Faculty.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "faculty");

            migrationBuilder.CreateTable(
                name: "course_assignments",
                schema: "faculty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_applied_event_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faculty_members",
                schema: "faculty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<string>(type: "text", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    designation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employment_type = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    is_department_head = table.Column<bool>(type: "boolean", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    joining_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculty_members", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_requests",
                schema: "faculty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    routed_directly_to_authority = table.Column<bool>(type: "boolean", nullable: false),
                    supporting_document_reference = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_requests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "faculty",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    PayloadJson = table.Column<string>(type: "text", nullable: false),
                    OccurredAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    RecordedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "processed_inbound_events",
                schema: "faculty",
                columns: table => new
                {
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_processed_inbound_events", x => x.event_id);
                });

            migrationBuilder.CreateTable(
                name: "research_profiles",
                schema: "faculty",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ongoing_research = table.Column<string>(type: "text", nullable: true),
                    grants = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "leave_request_reason_translations",
                schema: "faculty",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    leave_request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_leave_request_reason_translations", x => new { x.leave_request_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_leave_request_reason_translations_leave_requests_leave_requ~",
                        column: x => x.leave_request_id,
                        principalSchema: "faculty",
                        principalTable: "leave_requests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "research_profile_publications",
                schema: "faculty",
                columns: table => new
                {
                    research_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    title = table.Column<string>(type: "text", nullable: false),
                    venue = table.Column<string>(type: "text", nullable: false),
                    year = table.Column<int>(type: "integer", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_research_profile_publications", x => new { x.research_profile_id, x.Id });
                    table.ForeignKey(
                        name: "FK_research_profile_publications_research_profiles_research_pr~",
                        column: x => x.research_profile_id,
                        principalSchema: "faculty",
                        principalTable: "research_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_course_assignments_faculty_member_id",
                schema: "faculty",
                table: "course_assignments",
                column: "faculty_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_course_assignments_faculty_member_id_course_offering_id",
                schema: "faculty",
                table: "course_assignments",
                columns: new[] { "faculty_member_id", "course_offering_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faculty_members_department_id",
                schema: "faculty",
                table: "faculty_members",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ux_faculty_members_employee_id",
                schema: "faculty",
                table: "faculty_members",
                column: "employee_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_faculty_members_user_id",
                schema: "faculty",
                table: "faculty_members",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leave_requests_faculty_member_id",
                schema: "faculty",
                table: "leave_requests",
                column: "faculty_member_id");

            migrationBuilder.CreateIndex(
                name: "ux_research_profiles_faculty_member_id",
                schema: "faculty",
                table: "research_profiles",
                column: "faculty_member_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "course_assignments",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "faculty_members",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "leave_request_reason_translations",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "research_profile_publications",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "leave_requests",
                schema: "faculty");

            migrationBuilder.DropTable(
                name: "research_profiles",
                schema: "faculty");
        }
    }
}
