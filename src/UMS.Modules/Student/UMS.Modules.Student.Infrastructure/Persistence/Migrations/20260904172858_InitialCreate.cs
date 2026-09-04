using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Student.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "student");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,");

            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                schema: "student",
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
                name: "students",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    originating_application_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_number = table.Column<string>(type: "text", nullable: false),
                    admission_year = table.Column<int>(type: "integer", nullable: false),
                    faculty_code = table.Column<string>(type: "text", nullable: false),
                    student_number_sequence = table.Column<long>(type: "bigint", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    given_name = table.Column<string>(type: "text", nullable: false),
                    family_name = table.Column<string>(type: "text", nullable: false),
                    given_name_bn = table.Column<string>(type: "text", nullable: true),
                    family_name_bn = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: false),
                    mobile = table.Column<string>(type: "citext", nullable: true),
                    date_of_birth = table.Column<DateOnly>(type: "date", nullable: false),
                    national_id = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    identity_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    id_card_document_id = table.Column<Guid>(type: "uuid", nullable: true),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    photo_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_students", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "guardian_access_grants",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    guardian_id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardian_access_grants", x => x.id);
                    table.ForeignKey(
                        name: "FK_guardian_access_grants_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "student",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "guardians",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    relationship = table.Column<string>(type: "text", nullable: false),
                    contact_email = table.Column<string>(type: "text", nullable: true),
                    contact_phone = table.Column<string>(type: "text", nullable: true),
                    linked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guardians", x => x.id);
                    table.ForeignKey(
                        name: "FK_guardians_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "student",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "student_status_history",
                schema: "student",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_status = table.Column<string>(type: "text", nullable: true),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    changed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    student_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_status_history", x => x.id);
                    table.ForeignKey(
                        name: "FK_student_status_history_students_student_id",
                        column: x => x.student_id,
                        principalSchema: "student",
                        principalTable: "students",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_guardian_access_grants_student_guardian",
                schema: "student",
                table: "guardian_access_grants",
                columns: new[] { "student_id", "guardian_id" });

            migrationBuilder.CreateIndex(
                name: "ix_guardians_student_id",
                schema: "student",
                table: "guardians",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_student_status_history_student_id",
                schema: "student",
                table: "student_status_history",
                column: "student_id");

            migrationBuilder.CreateIndex(
                name: "ix_students_department_id",
                schema: "student",
                table: "students",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ux_students_identity_user_id",
                schema: "student",
                table: "students",
                column: "identity_user_id",
                unique: true,
                filter: "\"identity_user_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_students_originating_application_id",
                schema: "student",
                table: "students",
                column: "originating_application_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_students_student_number",
                schema: "student",
                table: "students",
                column: "student_number",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guardian_access_grants",
                schema: "student");

            migrationBuilder.DropTable(
                name: "guardians",
                schema: "student");

            migrationBuilder.DropTable(
                name: "OutboxMessages",
                schema: "student");

            migrationBuilder.DropTable(
                name: "student_status_history",
                schema: "student");

            migrationBuilder.DropTable(
                name: "students",
                schema: "student");
        }
    }
}
