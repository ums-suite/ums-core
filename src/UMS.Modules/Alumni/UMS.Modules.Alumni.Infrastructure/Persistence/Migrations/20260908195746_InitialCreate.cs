using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Alumni.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "alumni");

            migrationBuilder.CreateTable(
                name: "alumni",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id_ref = table.Column<Guid>(type: "uuid", nullable: false),
                    graduation_year = table.Column<int>(type: "integer", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    profile_visibility = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    current_employer = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    bio = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    hide_current_employer = table.Column<bool>(type: "boolean", nullable: false),
                    hide_contact_details = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alumni", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alumni_chapters",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    region = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alumni_chapters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alumni_event_rsvps",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumnus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    response = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    guest_count = table.Column<int>(type: "integer", nullable: false),
                    responded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alumni_event_rsvps", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "alumni_events",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    content_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alumni_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "donation_campaigns",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    goal_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    starts_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_early = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donation_campaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "donations",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    alumnus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    is_anonymous = table.Column<bool>(type: "boolean", nullable: false),
                    recurrence_interval = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recurrence_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    series_root_donation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_charge_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_donations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_applications",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_posting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_is_alumnus = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    resume_artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_postings",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    poster_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    poster_is_alumnus = table.Column<bool>(type: "boolean", nullable: false),
                    poster_alumnus_id = table.Column<Guid>(type: "uuid", nullable: true),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    company = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    contact_method = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    moderation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_postings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mentorship_matches",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentor_alumnus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mentee_student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    proposed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    mentor_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    mentee_accepted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ended_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentorship_matches", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "mentorship_opt_ins",
                schema: "alumni",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    expertise_areas = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    capacity_limit = table.Column<int>(type: "integer", nullable: false),
                    active_count = table.Column<int>(type: "integer", nullable: false),
                    availability = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_mentorship_opt_ins", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "alumni",
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
                schema: "alumni",
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
                name: "chapter_memberships",
                schema: "alumni",
                columns: table => new
                {
                    chapter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    alumnus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chapter_memberships", x => new { x.chapter_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_chapter_memberships_alumni_chapters_chapter_id",
                        column: x => x.chapter_id,
                        principalSchema: "alumni",
                        principalTable: "alumni_chapters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_alumni_department_id",
                schema: "alumni",
                table: "alumni",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumni_graduation_year",
                schema: "alumni",
                table: "alumni",
                column: "graduation_year");

            migrationBuilder.CreateIndex(
                name: "ix_alumni_profile_visibility",
                schema: "alumni",
                table: "alumni",
                column: "profile_visibility");

            migrationBuilder.CreateIndex(
                name: "ix_alumni_program_id",
                schema: "alumni",
                table: "alumni",
                column: "program_id");

            migrationBuilder.CreateIndex(
                name: "ux_alumni_student_id_ref",
                schema: "alumni",
                table: "alumni",
                column: "student_id_ref",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_alumni_event_rsvps_event_alumnus",
                schema: "alumni",
                table: "alumni_event_rsvps",
                columns: new[] { "event_id", "alumnus_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_alumni_events_chapter_id",
                schema: "alumni",
                table: "alumni_events",
                column: "chapter_id");

            migrationBuilder.CreateIndex(
                name: "ix_alumni_events_event_date",
                schema: "alumni",
                table: "alumni_events",
                column: "event_date");

            migrationBuilder.CreateIndex(
                name: "ux_chapter_memberships_chapter_alumnus",
                schema: "alumni",
                table: "chapter_memberships",
                columns: new[] { "chapter_id", "alumnus_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_donation_campaigns_window",
                schema: "alumni",
                table: "donation_campaigns",
                columns: new[] { "starts_at", "ends_at" });

            migrationBuilder.CreateIndex(
                name: "ix_donations_alumnus_id",
                schema: "alumni",
                table: "donations",
                column: "alumnus_id");

            migrationBuilder.CreateIndex(
                name: "ix_donations_campaign_id",
                schema: "alumni",
                table: "donations",
                column: "campaign_id");

            migrationBuilder.CreateIndex(
                name: "ix_donations_invoice_id",
                schema: "alumni",
                table: "donations",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_donations_recurrence_status_next_charge_at",
                schema: "alumni",
                table: "donations",
                columns: new[] { "recurrence_status", "next_charge_at" });

            migrationBuilder.CreateIndex(
                name: "ix_donations_series_root_donation_id",
                schema: "alumni",
                table: "donations",
                column: "series_root_donation_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_applicant_user_id",
                schema: "alumni",
                table: "job_applications",
                column: "applicant_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_applications_job_posting_id",
                schema: "alumni",
                table: "job_applications",
                column: "job_posting_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_poster_user_id",
                schema: "alumni",
                table: "job_postings",
                column: "poster_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_status",
                schema: "alumni",
                table: "job_postings",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_job_postings_status_expires_at",
                schema: "alumni",
                table: "job_postings",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mentorship_matches_mentee_student_id",
                schema: "alumni",
                table: "mentorship_matches",
                column: "mentee_student_id");

            migrationBuilder.CreateIndex(
                name: "ix_mentorship_matches_mentor_alumnus_id",
                schema: "alumni",
                table: "mentorship_matches",
                column: "mentor_alumnus_id");

            migrationBuilder.CreateIndex(
                name: "ix_mentorship_matches_status",
                schema: "alumni",
                table: "mentorship_matches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_mentorship_opt_ins_person_role",
                schema: "alumni",
                table: "mentorship_opt_ins",
                columns: new[] { "person_id", "role" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type_processed_at",
                schema: "alumni",
                table: "outbox_messages",
                columns: new[] { "event_type", "processed_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "alumni",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "alumni_event_rsvps",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "alumni_events",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "chapter_memberships",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "donation_campaigns",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "donations",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "job_applications",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "job_postings",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "mentorship_matches",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "mentorship_opt_ins",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "alumni");

            migrationBuilder.DropTable(
                name: "alumni_chapters",
                schema: "alumni");
        }
    }
}
