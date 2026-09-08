using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Research.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "research");

            migrationBuilder.CreateTable(
                name: "funding_bodies",
                schema: "research",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    website = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_funding_bodies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grants",
                schema: "research",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    funding_body_id = table.Column<Guid>(type: "uuid", nullable: false),
                    funding_period_start = table.Column<DateOnly>(type: "date", nullable: false),
                    funding_period_end = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    requires_pi_reassignment = table.Column<bool>(type: "boolean", nullable: false),
                    is_publicly_visible = table.Column<bool>(type: "boolean", nullable: false),
                    award_date = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    reported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    funding_amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    funding_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "institutional_repository_entries",
                schema: "research",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    work_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    depositor_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    depositor_faculty_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supervising_faculty_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deposit_date = table.Column<DateOnly>(type: "date", nullable: false),
                    is_embargoed = table.Column<bool>(type: "boolean", nullable: false),
                    embargo_end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    access_level = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    artifact_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_repository_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "research",
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
                schema: "research",
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
                name: "publication_duplicate_candidates",
                schema: "research",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    match_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    resolved_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publication_duplicate_candidates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "publications",
                schema: "research",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    venue_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    venue_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    venue_publisher = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    doi = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    publication_date = table.Column<DateOnly>(type: "date", nullable: false),
                    citation_count = table.Column<int>(type: "integer", nullable: true),
                    is_publicly_visible = table.Column<bool>(type: "boolean", nullable: false),
                    merged_into_publication_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    funded_by_grant_ids = table.Column<string>(type: "jsonb", nullable: false),
                    normalized_doi = table.Column<string>(type: "text", nullable: true, computedColumnSql: "lower(btrim(doi))", stored: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publications", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "grant_investigators",
                schema: "research",
                columns: table => new
                {
                    grant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    faculty_member_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_grant_investigators", x => new { x.grant_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_grant_investigators_grants_grant_id",
                        column: x => x.grant_id,
                        principalSchema: "research",
                        principalTable: "grants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "publication_authors",
                schema: "research",
                columns: table => new
                {
                    publication_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_order = table.Column<int>(type: "integer", nullable: false),
                    faculty_member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    affiliation = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_corresponding_author = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_publication_authors", x => new { x.publication_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_publication_authors_publications_publication_id",
                        column: x => x.publication_id,
                        principalSchema: "research",
                        principalTable: "publications",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_grant_investigators_grant_faculty_member",
                schema: "research",
                table: "grant_investigators",
                columns: new[] { "grant_id", "faculty_member_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_grants_funding_body_id",
                schema: "research",
                table: "grants",
                column: "funding_body_id");

            migrationBuilder.CreateIndex(
                name: "ix_grants_is_publicly_visible",
                schema: "research",
                table: "grants",
                column: "is_publicly_visible");

            migrationBuilder.CreateIndex(
                name: "ix_grants_status",
                schema: "research",
                table: "grants",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_institutional_repository_entries_embargo",
                schema: "research",
                table: "institutional_repository_entries",
                columns: new[] { "is_embargoed", "embargo_end_date" });

            migrationBuilder.CreateIndex(
                name: "ix_institutional_repository_entries_supervisor",
                schema: "research",
                table: "institutional_repository_entries",
                column: "supervising_faculty_member_id");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "research",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "research",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ix_publication_duplicate_candidates_publication_id",
                schema: "research",
                table: "publication_duplicate_candidates",
                column: "publication_id");

            migrationBuilder.CreateIndex(
                name: "ix_publication_duplicate_candidates_status",
                schema: "research",
                table: "publication_duplicate_candidates",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_publications_is_publicly_visible",
                schema: "research",
                table: "publications",
                column: "is_publicly_visible");

            migrationBuilder.CreateIndex(
                name: "ix_publications_merged_into_publication_id",
                schema: "research",
                table: "publications",
                column: "merged_into_publication_id");

            migrationBuilder.CreateIndex(
                name: "ux_publications_normalized_doi",
                schema: "research",
                table: "publications",
                column: "normalized_doi",
                unique: true,
                filter: "normalized_doi IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "funding_bodies",
                schema: "research");

            migrationBuilder.DropTable(
                name: "grant_investigators",
                schema: "research");

            migrationBuilder.DropTable(
                name: "institutional_repository_entries",
                schema: "research");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "research");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "research");

            migrationBuilder.DropTable(
                name: "publication_authors",
                schema: "research");

            migrationBuilder.DropTable(
                name: "publication_duplicate_candidates",
                schema: "research");

            migrationBuilder.DropTable(
                name: "grants",
                schema: "research");

            migrationBuilder.DropTable(
                name: "publications",
                schema: "research");
        }
    }
}
