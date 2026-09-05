using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Academic.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnrollmentDropAllowsReenrollment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_enrollments_student_courseoffering_semester",
                schema: "academic",
                table: "enrollments");

            migrationBuilder.CreateIndex(
                name: "ux_enrollments_student_courseoffering_semester",
                schema: "academic",
                table: "enrollments",
                columns: new[] { "student_id", "course_offering_id", "semester_id" },
                unique: true,
                filter: "status <> 'Dropped'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ux_enrollments_student_courseoffering_semester",
                schema: "academic",
                table: "enrollments");

            migrationBuilder.CreateIndex(
                name: "ux_enrollments_student_courseoffering_semester",
                schema: "academic",
                table: "enrollments",
                columns: new[] { "student_id", "course_offering_id", "semester_id" },
                unique: true);
        }
    }
}
