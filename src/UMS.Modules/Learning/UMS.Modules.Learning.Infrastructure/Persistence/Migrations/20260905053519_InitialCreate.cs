using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Learning.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "learning");

            migrationBuilder.CreateTable(
                name: "assignments",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    instructions = table.Column<string>(type: "text", nullable: false),
                    allowed_submission_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    allow_resubmission = table.Column<bool>(type: "boolean", nullable: false),
                    max_points = table.Column<int>(type: "integer", nullable: false),
                    window_opens_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    window_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    window_grace_period = table.Column<TimeSpan>(type: "interval", nullable: false),
                    window_late_penalty_tiers = table.Column<string>(type: "jsonb", nullable: false),
                    window_hard_close_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assignments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "discussion_threads",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discussion_threads", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "lecture_materials",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    module_group = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecture_materials", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "learning",
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
                name: "submissions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text_content = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    is_late = table.Column<bool>(type: "boolean", nullable: false),
                    late_penalty_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    applied_extension_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    superseded_by_submission_id = table.Column<Guid>(type: "uuid", nullable: true),
                    superseded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    score_raw_points = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    score_awarded_points = table.Column<decimal>(type: "numeric(9,2)", precision: 9, scale: 2, nullable: true),
                    score_max_points = table.Column<int>(type: "integer", nullable: true),
                    score_applied_late_penalty_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    score_feedback = table.Column<string>(type: "text", nullable: true),
                    evaluated_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submissions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "submission_extensions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    extended_deadline = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    granted_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    waives_late_penalty = table.Column<bool>(type: "boolean", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_extensions", x => x.id);
                    table.ForeignKey(
                        name: "FK_submission_extensions_assignments_assignment_id",
                        column: x => x.assignment_id,
                        principalSchema: "learning",
                        principalTable: "assignments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "discussion_posts",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    discussion_thread_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_post_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    moderation_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    removed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    removed_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    restored_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    restored_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_discussion_posts", x => x.id);
                    table.ForeignKey(
                        name: "FK_discussion_posts_discussion_threads_discussion_thread_id",
                        column: x => x.discussion_thread_id,
                        principalSchema: "learning",
                        principalTable: "discussion_threads",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lecture_material_translations",
                schema: "learning",
                columns: table => new
                {
                    lecture_material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_code = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecture_material_translations", x => new { x.lecture_material_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_lecture_material_translations_lecture_materials_lecture_mat~",
                        column: x => x.lecture_material_id,
                        principalSchema: "learning",
                        principalTable: "lecture_materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "lecture_material_versions",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    lecture_material_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_url = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    change_note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lecture_material_versions", x => x.id);
                    table.ForeignKey(
                        name: "FK_lecture_material_versions_lecture_materials_lecture_materia~",
                        column: x => x.lecture_material_id,
                        principalSchema: "learning",
                        principalTable: "lecture_materials",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "plagiarism_checks",
                schema: "learning",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    score_similarity_percentage = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    score_matched_source_summary = table.Column<string>(type: "text", nullable: true),
                    score_provider_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    queued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_plagiarism_checks", x => x.id);
                    table.ForeignKey(
                        name: "FK_plagiarism_checks_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "learning",
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "submission_files",
                schema: "learning",
                columns: table => new
                {
                    submission_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_submission_files", x => new { x.submission_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_submission_files_submissions_submission_id",
                        column: x => x.submission_id,
                        principalSchema: "learning",
                        principalTable: "submissions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_assignments_course_offering_id",
                schema: "learning",
                table: "assignments",
                column: "course_offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_assignments_status",
                schema: "learning",
                table: "assignments",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_discussion_posts_discussion_thread_id",
                schema: "learning",
                table: "discussion_posts",
                column: "discussion_thread_id");

            migrationBuilder.CreateIndex(
                name: "ix_discussion_threads_course_offering_id",
                schema: "learning",
                table: "discussion_threads",
                column: "course_offering_id");

            migrationBuilder.CreateIndex(
                name: "ux_lecture_material_versions_material_number",
                schema: "learning",
                table: "lecture_material_versions",
                columns: new[] { "lecture_material_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_lecture_materials_offering_group_order",
                schema: "learning",
                table: "lecture_materials",
                columns: new[] { "course_offering_id", "module_group", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "learning",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "learning",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_plagiarism_checks_status",
                schema: "learning",
                table: "plagiarism_checks",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "IX_plagiarism_checks_submission_id",
                schema: "learning",
                table: "plagiarism_checks",
                column: "submission_id");

            migrationBuilder.CreateIndex(
                name: "ux_submission_extensions_assignment_student",
                schema: "learning",
                table: "submission_extensions",
                columns: new[] { "assignment_id", "student_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_submissions_assignment_status",
                schema: "learning",
                table: "submissions",
                columns: new[] { "assignment_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_assignment_student",
                schema: "learning",
                table: "submissions",
                columns: new[] { "assignment_id", "student_id" });

            migrationBuilder.CreateIndex(
                name: "ix_submissions_course_offering_id",
                schema: "learning",
                table: "submissions",
                column: "course_offering_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "discussion_posts",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "lecture_material_translations",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "lecture_material_versions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "plagiarism_checks",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "submission_extensions",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "submission_files",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "discussion_threads",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "lecture_materials",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "assignments",
                schema: "learning");

            migrationBuilder.DropTable(
                name: "submissions",
                schema: "learning");
        }
    }
}
