using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Admission.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "admission");

            migrationBuilder.CreateSequence(
                name: "application_number_seq",
                schema: "admission");

            migrationBuilder.CreateTable(
                name: "admission_campaigns",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    program_ids = table.Column<string>(type: "jsonb", nullable: false),
                    application_window_start = table.Column<DateOnly>(type: "date", nullable: false),
                    application_window_end = table.Column<DateOnly>(type: "date", nullable: false),
                    application_fee_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    confirmation_fee_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    required_document_types = table.Column<string>(type: "jsonb", nullable: false),
                    is_configuration_locked = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_campaigns", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admission_results",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merit_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    calculated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    publishing_started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    published_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    correction_count = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "admission_tests",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_tests", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "applicants",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    given_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    family_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    mobile = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    is_email_verified = table.Column<bool>(type: "boolean", nullable: false),
                    is_mobile_verified = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    pending_otp_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    pending_otp_channel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    pending_otp_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    pending_otp_attempts = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applicants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "applications",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    application_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    application_fee_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_application_fee_paid = table.Column<bool>(type: "boolean", nullable: false),
                    confirmation_fee_invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_confirmation_fee_paid = table.Column<bool>(type: "boolean", nullable: false),
                    assigned_test_slot_id = table.Column<Guid>(type: "uuid", nullable: true),
                    roll_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    admit_card_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    locked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempts",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    test_slot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roll_number = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    proctoring_session_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    seed_value = table.Column<long>(type: "bigint", nullable: false),
                    selected_question_ids = table.Column<string>(type: "jsonb", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    submission_source = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    evaluation_status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    objective_score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    subjective_score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempts", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "merit_lists",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    generated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    approved_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merit_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "admission",
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
                schema: "admission",
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
                name: "publish_jobs",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    total_count = table.Column<int>(type: "integer", nullable: false),
                    cache_processed_count = table.Column<int>(type: "integer", nullable: false),
                    cache_cursor = table.Column<Guid>(type: "uuid", nullable: true),
                    fan_out_processed_count = table.Column<int>(type: "integer", nullable: false),
                    fan_out_cursor = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publish_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "campaign_eligibility_rules",
                schema: "admission",
                columns: table => new
                {
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    min_score_scale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    required_board = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_eligibility_rules", x => new { x.campaign_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_campaign_eligibility_rules_admission_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "admission",
                        principalTable: "admission_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "campaign_seat_quotas",
                schema: "admission",
                columns: table => new
                {
                    campaign_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quota = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campaign_seat_quotas", x => new { x.campaign_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_campaign_seat_quotas_admission_campaigns_campaign_id",
                        column: x => x.campaign_id,
                        principalSchema: "admission",
                        principalTable: "admission_campaigns",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admission_result_entries",
                schema: "admission",
                columns: table => new
                {
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    admission_result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    merit_rank = table.Column<int>(type: "integer", nullable: false),
                    waitlist_rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_result_entries", x => new { x.admission_result_id, x.applicant_id });
                    table.ForeignKey(
                        name: "FK_admission_result_entries_admission_results_admission_result~",
                        column: x => x.admission_result_id,
                        principalSchema: "admission",
                        principalTable: "admission_results",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admission_test_questions",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: false),
                    correct_option_index = table.Column<int>(type: "integer", nullable: true),
                    is_subjective = table.Column<bool>(type: "boolean", nullable: false),
                    max_score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    admission_test_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_test_questions", x => x.id);
                    table.ForeignKey(
                        name: "FK_admission_test_questions_admission_tests_admission_test_id",
                        column: x => x.admission_test_id,
                        principalSchema: "admission",
                        principalTable: "admission_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admission_test_selection_rules",
                schema: "admission",
                columns: table => new
                {
                    admission_test_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_test_selection_rules", x => new { x.admission_test_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_admission_test_selection_rules_admission_tests_admission_te~",
                        column: x => x.admission_test_id,
                        principalSchema: "admission",
                        principalTable: "admission_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "admission_test_slots",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: false),
                    remaining_seats = table.Column<int>(type: "integer", nullable: false),
                    admission_test_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admission_test_slots", x => x.id);
                    table.ForeignKey(
                        name: "FK_admission_test_slots_admission_tests_admission_test_id",
                        column: x => x.admission_test_id,
                        principalSchema: "admission",
                        principalTable: "admission_tests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "applicant_academic_records",
                schema: "admission",
                columns: table => new
                {
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    board = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    exam_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    passing_year = table.Column<int>(type: "integer", nullable: false),
                    score = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    score_scale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_applicant_academic_records", x => new { x.applicant_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_applicant_academic_records_applicants_applicant_id",
                        column: x => x.applicant_id,
                        principalSchema: "admission",
                        principalTable: "applicants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_documents",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    file_reference = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_application_documents_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "admission",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "application_program_choices",
                schema: "admission",
                columns: table => new
                {
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_application_program_choices", x => new { x.application_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_application_program_choices_applications_application_id",
                        column: x => x.application_id,
                        principalSchema: "admission",
                        principalTable: "applications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempt_answers",
                schema: "admission",
                columns: table => new
                {
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    question_id = table.Column<Guid>(type: "uuid", nullable: false),
                    selected_option_index = table.Column<int>(type: "integer", nullable: true),
                    subjective_text = table.Column<string>(type: "text", nullable: true),
                    answered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempt_answers", x => new { x.exam_attempt_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_exam_attempt_answers_exam_attempts_exam_attempt_id",
                        column: x => x.exam_attempt_id,
                        principalSchema: "admission",
                        principalTable: "exam_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "exam_attempt_integrity_flags",
                schema: "admission",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    anomaly_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    confidence_score = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: false),
                    raised_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reviewed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    review_notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    exam_attempt_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_exam_attempt_integrity_flags", x => x.id);
                    table.ForeignKey(
                        name: "FK_exam_attempt_integrity_flags_exam_attempts_exam_attempt_id",
                        column: x => x.exam_attempt_id,
                        principalSchema: "admission",
                        principalTable: "exam_attempts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merit_list_entries",
                schema: "admission",
                columns: table => new
                {
                    applicant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    merit_list_id = table.Column<Guid>(type: "uuid", nullable: false),
                    application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    score = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                    rank = table.Column<int>(type: "integer", nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    waitlist_rank = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_merit_list_entries", x => new { x.merit_list_id, x.applicant_id });
                    table.ForeignKey(
                        name: "FK_merit_list_entries_merit_lists_merit_list_id",
                        column: x => x.merit_list_id,
                        principalSchema: "admission",
                        principalTable: "merit_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_admission_results_campaign_id",
                schema: "admission",
                table: "admission_results",
                column: "campaign_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admission_test_questions_admission_test_id",
                schema: "admission",
                table: "admission_test_questions",
                column: "admission_test_id");

            migrationBuilder.CreateIndex(
                name: "IX_admission_test_selection_rules_admission_test_id_difficulty",
                schema: "admission",
                table: "admission_test_selection_rules",
                columns: new[] { "admission_test_id", "difficulty" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admission_test_slots_admission_test_id",
                schema: "admission",
                table: "admission_test_slots",
                column: "admission_test_id");

            migrationBuilder.CreateIndex(
                name: "ux_admission_tests_campaign_id",
                schema: "admission",
                table: "admission_tests",
                column: "campaign_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_applicants_identity_user_id",
                schema: "admission",
                table: "applicants",
                column: "identity_user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_application_documents_application_id",
                schema: "admission",
                table: "application_documents",
                column: "application_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_application_fee_invoice_id",
                schema: "admission",
                table: "applications",
                column: "application_fee_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_confirmation_fee_invoice_id",
                schema: "admission",
                table: "applications",
                column: "confirmation_fee_invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_applications_status",
                schema: "admission",
                table: "applications",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ux_applications_applicant_campaign",
                schema: "admission",
                table: "applications",
                columns: new[] { "applicant_id", "campaign_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_applications_application_number",
                schema: "admission",
                table: "applications",
                column: "application_number",
                unique: true,
                filter: "application_number IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_campaign_seat_quotas_campaign_id_program_id",
                schema: "admission",
                table: "campaign_seat_quotas",
                columns: new[] { "campaign_id", "program_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempt_answers_exam_attempt_id_question_id",
                schema: "admission",
                table: "exam_attempt_answers",
                columns: new[] { "exam_attempt_id", "question_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempt_integrity_flags_exam_attempt_id",
                schema: "admission",
                table: "exam_attempt_integrity_flags",
                column: "exam_attempt_id");

            migrationBuilder.CreateIndex(
                name: "ix_exam_attempts_status_expires_at",
                schema: "admission",
                table: "exam_attempts",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "ux_exam_attempts_applicant_test",
                schema: "admission",
                table: "exam_attempts",
                columns: new[] { "applicant_id", "admission_test_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_merit_lists_campaign_id",
                schema: "admission",
                table: "merit_lists",
                column: "campaign_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "admission",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "admission",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_publish_jobs_admission_result_id",
                schema: "admission",
                table: "publish_jobs",
                column: "admission_result_id");

            migrationBuilder.CreateIndex(
                name: "ix_publish_jobs_stage",
                schema: "admission",
                table: "publish_jobs",
                column: "stage");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admission_result_entries",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_test_questions",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_test_selection_rules",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_test_slots",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "applicant_academic_records",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "application_documents",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "application_program_choices",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "campaign_eligibility_rules",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "campaign_seat_quotas",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "exam_attempt_answers",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "exam_attempt_integrity_flags",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "merit_list_entries",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "publish_jobs",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_results",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_tests",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "applicants",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "applications",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "admission_campaigns",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "exam_attempts",
                schema: "admission");

            migrationBuilder.DropTable(
                name: "merit_lists",
                schema: "admission");

            migrationBuilder.DropSequence(
                name: "application_number_seq",
                schema: "admission");
        }
    }
}
