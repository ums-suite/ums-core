using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Career.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "career");

            migrationBuilder.CreateTable(
                name: "campus_recruitment_drives",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employer_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    venue_room_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scheduled_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registration_opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    registration_closes_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campus_recruitment_drives", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "career_applications",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    internship_id = table.Column<Guid>(type: "uuid", nullable: true),
                    drive_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    declared_cgpa = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    declared_year_of_study = table.Column<int>(type: "integer", nullable: true),
                    resume_profile_id_snapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_artifact_id_snapshot = table.Column<Guid>(type: "uuid", nullable: false),
                    resume_file_name_snapshot = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    resume_snapshot_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    interview_slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decision_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_career_applications", x => x.id);
                    table.CheckConstraint("ck_career_applications_target_xor", "(internship_id IS NOT NULL AND drive_id IS NULL) OR (internship_id IS NULL AND drive_id IS NOT NULL)");
                });

            migrationBuilder.CreateTable(
                name: "employer_profiles",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    company_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    industry = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    contact_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    contact_email = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    verification_note = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_archived = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employer_profiles", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "internships",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employer_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    stipend_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    application_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    withdrawal_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    eligibility_min_cgpa = table.Column<decimal>(type: "numeric(3,2)", nullable: true),
                    eligibility_min_year_of_study = table.Column<int>(type: "integer", nullable: true),
                    eligibility_program_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_internships", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "interview_slots",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    drive_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    booked_count = table.Column<int>(type: "integer", nullable: false),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_interview_slots", x => x.id);
                    table.CheckConstraint("ck_interview_slots_capacity", "booked_count <= capacity AND booked_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
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
                name: "processed_inbound_events",
                schema: "career",
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
                name: "resume_profiles",
                schema: "career",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resume_profiles", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_campus_recruitment_drives_employer_profile_id",
                schema: "career",
                table: "campus_recruitment_drives",
                column: "employer_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_campus_recruitment_drives_status",
                schema: "career",
                table: "campus_recruitment_drives",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_career_applications_drive_id",
                schema: "career",
                table: "career_applications",
                column: "drive_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_applications_internship_id",
                schema: "career",
                table: "career_applications",
                column: "internship_id");

            migrationBuilder.CreateIndex(
                name: "ix_career_applications_student_id",
                schema: "career",
                table: "career_applications",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_career_applications_student_drive",
                schema: "career",
                table: "career_applications",
                columns: new[] { "student_id", "drive_id" },
                unique: true,
                filter: "drive_id IS NOT NULL AND status NOT IN ('Withdrawn', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ux_career_applications_student_internship",
                schema: "career",
                table: "career_applications",
                columns: new[] { "student_id", "internship_id" },
                unique: true,
                filter: "internship_id IS NOT NULL AND status NOT IN ('Withdrawn', 'Cancelled')");

            migrationBuilder.CreateIndex(
                name: "ix_employer_profiles_is_archived",
                schema: "career",
                table: "employer_profiles",
                column: "is_archived");

            migrationBuilder.CreateIndex(
                name: "ix_internships_employer_profile_id",
                schema: "career",
                table: "internships",
                column: "employer_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_internships_status",
                schema: "career",
                table: "internships",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_internships_status_deadline",
                schema: "career",
                table: "internships",
                columns: new[] { "status", "application_deadline" });

            migrationBuilder.CreateIndex(
                name: "ix_interview_slots_drive_id",
                schema: "career",
                table: "interview_slots",
                column: "drive_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type_processed_at",
                schema: "career",
                table: "outbox_messages",
                columns: new[] { "event_type", "processed_at" });

            migrationBuilder.CreateIndex(
                name: "ix_resume_profiles_student_id",
                schema: "career",
                table: "resume_profiles",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_resume_profiles_student_default",
                schema: "career",
                table: "resume_profiles",
                columns: new[] { "student_id", "is_default" },
                unique: true,
                filter: "is_default = true AND is_deleted = false");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "campus_recruitment_drives",
                schema: "career");

            migrationBuilder.DropTable(
                name: "career_applications",
                schema: "career");

            migrationBuilder.DropTable(
                name: "employer_profiles",
                schema: "career");

            migrationBuilder.DropTable(
                name: "internships",
                schema: "career");

            migrationBuilder.DropTable(
                name: "interview_slots",
                schema: "career");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "career");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "career");

            migrationBuilder.DropTable(
                name: "resume_profiles",
                schema: "career");
        }
    }
}
