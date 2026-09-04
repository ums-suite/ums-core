using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Organization.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organization");

            migrationBuilder.CreateTable(
                name: "buildings",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buildings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "campuses",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    university_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_campuses", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "designations",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "faculties",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    campus_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculties", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
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
                name: "programs",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_programs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "rooms",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    building_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    room_type = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_rooms", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "universities",
                schema: "organization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    code = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_universities", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "department_translations",
                schema: "organization",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_translations", x => new { x.department_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_department_translations_departments_department_id",
                        column: x => x.department_id,
                        principalSchema: "organization",
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "designation_translations",
                schema: "organization",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    designation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_designation_translations", x => new { x.designation_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_designation_translations_designations_designation_id",
                        column: x => x.designation_id,
                        principalSchema: "organization",
                        principalTable: "designations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "faculty_translations",
                schema: "organization",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    faculty_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_faculty_translations", x => new { x.faculty_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_faculty_translations_faculties_faculty_id",
                        column: x => x.faculty_id,
                        principalSchema: "organization",
                        principalTable: "faculties",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "program_translations",
                schema: "organization",
                columns: table => new
                {
                    language_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    program_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_program_translations", x => new { x.program_id, x.language_code });
                    table.ForeignKey(
                        name: "FK_program_translations_programs_program_id",
                        column: x => x.program_id,
                        principalSchema: "organization",
                        principalTable: "programs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_buildings_campus_id",
                schema: "organization",
                table: "buildings",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ix_campuses_university_id",
                schema: "organization",
                table: "campuses",
                column: "university_id");

            migrationBuilder.CreateIndex(
                name: "ux_campuses_university_id_name",
                schema: "organization",
                table: "campuses",
                columns: new[] { "university_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_departments_faculty_id",
                schema: "organization",
                table: "departments",
                column: "faculty_id");

            migrationBuilder.CreateIndex(
                name: "ux_departments_faculty_id_name",
                schema: "organization",
                table: "departments",
                columns: new[] { "faculty_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_faculties_campus_id",
                schema: "organization",
                table: "faculties",
                column: "campus_id");

            migrationBuilder.CreateIndex(
                name: "ux_faculties_campus_id_name",
                schema: "organization",
                table: "faculties",
                columns: new[] { "campus_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_programs_department_id",
                schema: "organization",
                table: "programs",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "ux_programs_department_id_name",
                schema: "organization",
                table: "programs",
                columns: new[] { "department_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_rooms_building_id",
                schema: "organization",
                table: "rooms",
                column: "building_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "buildings",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "campuses",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "department_translations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "designation_translations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "faculty_translations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "program_translations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "rooms",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "universities",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "departments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "designations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "faculties",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "programs",
                schema: "organization");
        }
    }
}
