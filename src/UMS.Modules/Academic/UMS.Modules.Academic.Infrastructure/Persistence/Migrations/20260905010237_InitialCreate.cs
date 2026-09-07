using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "academic");

            migrationBuilder.CreateTable(
                name: "academic_sessions",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attendance_sessions",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    session_date = table.Column<DateOnly>(type: "date", nullable: false),
                    correction_window_close = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_sessions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "course_offerings",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    enrolled_count = table.Column<int>(type: "integer", nullable: false),
                    instructor_faculty_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    instructor_assigned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_offerings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "courses",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    credit_hours = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_courses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "curricula",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    curriculum_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curricula", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "enrollments",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    semester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    section_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    credit_hours_at_enrollment = table.Column<int>(type: "integer", nullable: false),
                    prerequisite_override_reason = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    dropped_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    drop_reason = table.Column<string>(type: "text", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_enrollments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "academic",
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
                name: "programs",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    max_credits_per_semester = table.Column<int>(type: "integer", nullable: false),
                    requires_advisor_approval = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "result_publications",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rejected_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    rejection_reason = table.Column<string>(type: "text", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correction_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_result_publications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "semesters",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    academic_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    registration_start = table.Column<DateOnly>(type: "date", nullable: false),
                    registration_end = table.Column<DateOnly>(type: "date", nullable: false),
                    drop_start = table.Column<DateOnly>(type: "date", nullable: false),
                    drop_end = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_semesters", x => x.id);
                    table.ForeignKey(
                        name: "FK_semesters_academic_sessions_academic_session_id",
                        column: x => x.academic_session_id,
                        principalSchema: "academic",
                        principalTable: "academic_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attendance_records",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attendance_session_id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    marked_by_faculty_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    marked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attendance_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_attendance_records_attendance_sessions_attendance_session_id",
                        column: x => x.attendance_session_id,
                        principalSchema: "academic",
                        principalTable: "attendance_sessions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exams",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exams", x => x.id);
                    table.ForeignKey(
                        name: "FK_exams_course_offerings_course_offering_id",
                        column: x => x.course_offering_id,
                        principalSchema: "academic",
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sections",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_offering_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    start_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false),
                    end_time = table.Column<TimeOnly>(type: "time without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sections", x => x.id);
                    table.ForeignKey(
                        name: "FK_sections_course_offerings_course_offering_id",
                        column: x => x.course_offering_id,
                        principalSchema: "academic",
                        principalTable: "course_offerings",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "course_prerequisites",
                schema: "academic",
                columns: table => new
                {
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    prerequisite_course_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_course_prerequisites", x => new { x.course_id, x.prerequisite_course_id });
                    table.ForeignKey(
                        name: "FK_course_prerequisites_courses_course_id",
                        column: x => x.course_id,
                        principalSchema: "academic",
                        principalTable: "courses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_courses",
                schema: "academic",
                columns: table => new
                {
                    curriculum_id = table.Column<Guid>(type: "uuid", nullable: false),
                    course_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_courses", x => new { x.curriculum_id, x.course_id });
                    table.ForeignKey(
                        name: "FK_curriculum_courses_curricula_curriculum_id",
                        column: x => x.curriculum_id,
                        principalSchema: "academic",
                        principalTable: "curricula",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grades",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    enrollment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    calculated_score_value = table.Column<decimal>(type: "numeric(6,2)", nullable: true),
                    calculated_score_scale = table.Column<string>(type: "text", nullable: true),
                    letter_grade = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submitted_by_user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grades", x => x.id);
                    table.ForeignKey(
                        name: "FK_grades_enrollments_enrollment_id",
                        column: x => x.enrollment_id,
                        principalSchema: "academic",
                        principalTable: "enrollments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessments",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    exam_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    weight = table.Column<decimal>(type: "numeric(5,4)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessments", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessments_exams_exam_id",
                        column: x => x.exam_id,
                        principalSchema: "academic",
                        principalTable: "exams",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assessment_score_entries",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assessment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assessment_score_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_assessment_score_entries_grades_grade_id",
                        column: x => x.grade_id,
                        principalSchema: "academic",
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "grade_correction_entries",
                schema: "academic",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    grade_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_score = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    new_score = table.Column<decimal>(type: "numeric(6,2)", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    corrected_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    corrected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grade_correction_entries", x => x.id);
                    table.ForeignKey(
                        name: "FK_grade_correction_entries_grades_grade_id",
                        column: x => x.grade_id,
                        principalSchema: "academic",
                        principalTable: "grades",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_academic_sessions_code",
                schema: "academic",
                table: "academic_sessions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_assessment_score_entries_grade_id",
                schema: "academic",
                table: "assessment_score_entries",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ix_assessments_exam_id",
                schema: "academic",
                table: "assessments",
                column: "exam_id");

            migrationBuilder.CreateIndex(
                name: "ux_attendance_records_session_enrollment",
                schema: "academic",
                table: "attendance_records",
                columns: new[] { "attendance_session_id", "enrollment_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attendance_sessions_courseoffering_date",
                schema: "academic",
                table: "attendance_sessions",
                columns: new[] { "course_offering_id", "session_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_course_offerings_semester_id",
                schema: "academic",
                table: "course_offerings",
                column: "semester_id");

            migrationBuilder.CreateIndex(
                name: "ux_courses_code",
                schema: "academic",
                table: "courses",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_curricula_program_version",
                schema: "academic",
                table: "curricula",
                columns: new[] { "program_id", "curriculum_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_course_offering_id",
                schema: "academic",
                table: "enrollments",
                column: "course_offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_enrollments_student_semester",
                schema: "academic",
                table: "enrollments",
                columns: new[] { "student_id", "semester_id" });

            migrationBuilder.CreateIndex(
                name: "ux_enrollments_student_courseoffering_semester",
                schema: "academic",
                table: "enrollments",
                columns: new[] { "student_id", "course_offering_id", "semester_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_exams_course_offering_id",
                schema: "academic",
                table: "exams",
                column: "course_offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_grade_correction_entries_grade_id",
                schema: "academic",
                table: "grade_correction_entries",
                column: "grade_id");

            migrationBuilder.CreateIndex(
                name: "ux_grades_enrollment_id",
                schema: "academic",
                table: "grades",
                column: "enrollment_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "academic",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "academic",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_programs_department_id",
                schema: "academic",
                table: "programs",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ux_programs_code",
                schema: "academic",
                table: "programs",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_result_publications_course_offering_id",
                schema: "academic",
                table: "result_publications",
                column: "course_offering_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_sections_course_offering_id",
                schema: "academic",
                table: "sections",
                column: "course_offering_id");

            migrationBuilder.CreateIndex(
                name: "ix_semesters_academic_session_id",
                schema: "academic",
                table: "semesters",
                column: "academic_session_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "assessment_score_entries",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "assessments",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "attendance_records",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "course_prerequisites",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "curriculum_courses",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "grade_correction_entries",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "programs",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "result_publications",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "sections",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "semesters",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "exams",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "attendance_sessions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "courses",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "curricula",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "grades",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "academic_sessions",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "course_offerings",
                schema: "academic");

            migrationBuilder.DropTable(
                name: "enrollments",
                schema: "academic");
        }
    }
}
