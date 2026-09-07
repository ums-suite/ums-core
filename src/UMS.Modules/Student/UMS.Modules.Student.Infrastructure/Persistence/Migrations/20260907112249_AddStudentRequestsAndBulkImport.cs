using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Student.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStudentRequestsAndBulkImport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "student_bulk_import_jobs",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    total_rows = table.Column<int>(type: "integer", nullable: false),
                    valid_row_count = table.Column<int>(type: "integer", nullable: false),
                    invalid_row_count = table.Column<int>(type: "integer", nullable: false),
                    processed_count = table.Column<int>(type: "integer", nullable: false),
                    succeeded_count = table.Column<int>(type: "integer", nullable: false),
                    failed_count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    approved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_bulk_import_jobs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_bulk_import_rows",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_id = table.Column<Guid>(type: "uuid", nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    payload_json = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    error_message = table.Column<string>(type: "text", nullable: true),
                    result_student_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_bulk_import_rows", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "student_requests",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_type = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    review_scope_node_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_against_own_department_head = table.Column<bool>(type: "boolean", nullable: false),
                    generated_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    decision_reason = table.Column<string>(type: "text", nullable: true),
                    submitted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    fulfilled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_requests", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_student_bulk_import_jobs_status",
                schema: "student",
                table: "student_bulk_import_jobs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_student_bulk_import_rows_job_status",
                schema: "student",
                table: "student_bulk_import_rows",
                columns: new[] { "job_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_student_bulk_import_rows_job_row_number",
                schema: "student",
                table: "student_bulk_import_rows",
                columns: new[] { "job_id", "row_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_student_requests_review_scope_node_id",
                schema: "student",
                table: "student_requests",
                column: "review_scope_node_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_requests_student_id",
                schema: "student",
                table: "student_requests",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ux_student_requests_student_id_request_type_open",
                schema: "student",
                table: "student_requests",
                columns: new[] { "student_id", "request_type" },
                unique: true,
                filter: "status IN ('Submitted', 'UnderReview')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "student_bulk_import_jobs",
                schema: "student");

            migrationBuilder.DropTable(
                name: "student_bulk_import_rows",
                schema: "student");

            migrationBuilder.DropTable(
                name: "student_requests",
                schema: "student");
        }
    }
}
