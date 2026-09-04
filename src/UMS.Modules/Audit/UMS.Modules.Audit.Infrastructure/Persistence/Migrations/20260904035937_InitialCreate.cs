using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Audit.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_export_requests",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    filter_json = table.Column<string>(type: "jsonb", nullable: false),
                    format = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    result_object_key = table.Column<string>(type: "text", nullable: true),
                    error_message = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_export_requests", x => x.id);
                });

            // audit_log_entries is hand-authored raw SQL, not migrationBuilder.CreateTable, because
            // EF Core's migration DSL has no concept of Postgres declarative partitioning
            // (AUD-3; requirement-spec.md audit §5/§9.1: monthly range-partitioning on
            // occurred_at). The column/constraint shape below matches AuditLogEntryConfiguration
            // exactly - keep the two in lockstep by hand for every future migration that touches
            // this table. The composite primary key (occurred_at, id) - rather than id alone - is
            // a hard Postgres requirement: every unique constraint/primary key on a partitioned
            // table must include the partition key column.
            //
            // Deliberately no partitions are created here: AuditPartitionMaintenanceService
            // creates the current operating window's partitions idempotently every time the Host
            // starts up (DependencyInjection.UseAuditModuleAsync), which is what actually needs to
            // track "now" - a migration authored once, long before an eventual deploy date, cannot
            // know what the current month will be when it actually runs.
            migrationBuilder.Sql("""
                CREATE TABLE audit.audit_log_entries (
                    id varchar(26) NOT NULL,
                    occurred_at timestamp with time zone NOT NULL,
                    actor_id text NOT NULL,
                    actor_type text NOT NULL,
                    ip_address text NULL,
                    application text NOT NULL,
                    entity_type text NOT NULL,
                    entity_id text NOT NULL,
                    action text NOT NULL,
                    before_value_json jsonb NULL,
                    after_value_json jsonb NULL,
                    correlation_id text NOT NULL,
                    reason text NULL,
                    organization_scope_id uuid NULL,
                    CONSTRAINT "PK_audit_log_entries" PRIMARY KEY (occurred_at, id)
                ) PARTITION BY RANGE (occurred_at);
                """);

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "audit",
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

            migrationBuilder.CreateIndex(
                name: "ix_audit_export_requests_status",
                schema: "audit",
                table: "audit_export_requests",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_actor",
                schema: "audit",
                table: "audit_log_entries",
                columns: new[] { "actor_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_correlation_id",
                schema: "audit",
                table: "audit_log_entries",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_entity",
                schema: "audit",
                table: "audit_log_entries",
                columns: new[] { "entity_type", "entity_id", "id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_entries_id",
                schema: "audit",
                table: "audit_log_entries",
                column: "id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_outbox_messages_processed_at",
                schema: "audit",
                table: "outbox_messages",
                column: "processed_at");

            // AUD-2: write-once database-level enforcement, defense in depth alongside the
            // API-layer omission (no update/delete endpoint or method anywhere in Audit's public
            // interface - see AuditLogEntry's own remarks). audit_service is granted INSERT/SELECT
            // only on this schema's tables - UPDATE/DELETE/TRUNCATE are never granted at all, so
            // Postgres itself rejects those statements regardless of what application code
            // attempts (requirement-spec.md audit §2 Write-Once Enforcement).
            //
            // Honest caveat, not papered over: this migration runs as whichever role owns the
            // `audit` schema's tables (the platform's single shared connection role today, ums.dev
            // local default) - and Postgres table OWNERS always bypass GRANT/REVOKE ACL checks
            // entirely, by design, regardless of what is revoked here. This REVOKE is therefore a
            // real, load-bearing control only once the platform's app runtime connects as a
            // DIFFERENT, non-owner role than whatever runs migrations - a standard production
            // hardening step (migrate as an elevated/owner role, run as a separate least-privilege
            // role) that is intentionally out of scope for this module alone to introduce
            // platform-wide. audit_service is created NOLOGIN (a privilege-carrying group role,
            // never a directly-connectable account with a password baked into migration history,
            // per ums-conventions.md's Secrets rule) - granting it to the platform's actual
            // non-owner runtime role, once one exists, is the remaining one-time operational step
            // that makes this control effective end-to-end.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'audit_service') THEN
                        CREATE ROLE audit_service NOLOGIN;
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA audit TO audit_service;
                GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA audit TO audit_service;
                ALTER DEFAULT PRIVILEGES IN SCHEMA audit GRANT SELECT, INSERT ON TABLES TO audit_service;
                REVOKE UPDATE, DELETE, TRUNCATE ON ALL TABLES IN SCHEMA audit FROM audit_service;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_export_requests",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "audit_log_entries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "audit");
        }
    }
}
