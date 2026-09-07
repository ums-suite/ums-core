using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "fee_structures",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    applicability_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    applicability_reference_id = table.Column<Guid>(type: "uuid", nullable: true),
                    applicability_service_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    effective_to = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fee_structures", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    fee_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "ledger_entries",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    reference_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ledger_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "finance",
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
                name: "payments",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_module = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_reference_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    initiated_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    idempotency_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "invoice_items",
                schema: "finance",
                columns: table => new
                {
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    fee_structure_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fee_structure_version = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoice_items", x => new { x.invoice_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_invoice_items_invoices_invoice_id",
                        column: x => x.invoice_id,
                        principalSchema: "finance",
                        principalTable: "invoices",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "payment_transactions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attempt_number = table.Column<int>(type: "integer", nullable: false),
                    gateway_session_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    gateway_transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_transactions", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_transactions_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "finance",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ux_fee_structures_active",
                schema: "finance",
                table: "fee_structures",
                columns: new[] { "fee_type", "applicability_type", "applicability_reference_id", "applicability_service_name" },
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_invoices_owner_id",
                schema: "finance",
                table: "invoices",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ux_invoices_natural_key",
                schema: "finance",
                table: "invoices",
                columns: new[] { "source_module", "source_reference_id", "fee_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_occurred_at",
                schema: "finance",
                table: "ledger_entries",
                column: "occurred_at");

            migrationBuilder.CreateIndex(
                name: "ix_ledger_entries_reference",
                schema: "finance",
                table: "ledger_entries",
                columns: new[] { "reference_type", "reference_id" });

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "finance",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "finance",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "IX_payment_transactions_payment_id",
                schema: "finance",
                table: "payment_transactions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ux_payment_transactions_gateway_transaction_id",
                schema: "finance",
                table: "payment_transactions",
                column: "gateway_transaction_id",
                unique: true,
                filter: "gateway_transaction_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_payments_owner_id",
                schema: "finance",
                table: "payments",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_payments_status_updated_at",
                schema: "finance",
                table: "payments",
                columns: new[] { "status", "updated_at" });

            migrationBuilder.CreateIndex(
                name: "ux_payments_idempotency_key",
                schema: "finance",
                table: "payments",
                column: "idempotency_key",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_payments_invoice_nonterminal",
                schema: "finance",
                table: "payments",
                column: "invoice_id",
                unique: true,
                filter: "status IN ('Initiated', 'Pending')");

            // requirement-spec.md finance §4: "LedgerEntry rows are immutable: no UPDATE/DELETE at
            // the API layer or the database GRANT level (mirrors the append-only, tamper-evident
            // discipline ADR-0012 establishes for Audit)." Identical mechanism to Audit's own
            // InitialCreate migration (AUD-1's write-once AuditLogEntry lockdown): a NOLOGIN
            // group role granted SELECT+INSERT only, REVOKE'd of UPDATE/DELETE/TRUNCATE. Same known
            // gap Audit's own migration documents rather than silently glossing over: today the app
            // connects as the schema-owning role, which bypasses ACL checks entirely (Postgres table
            // owners always do) - this REVOKE becomes load-bearing only once the platform's runtime
            // connects as a genuine non-owner role, the same one-time operational step Audit's own
            // migration already flags as outside any one module's build.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT FROM pg_catalog.pg_roles WHERE rolname = 'finance_service') THEN
                        CREATE ROLE finance_service NOLOGIN;
                    END IF;
                END
                $$;

                GRANT USAGE ON SCHEMA finance TO finance_service;
                GRANT SELECT, INSERT ON ALL TABLES IN SCHEMA finance TO finance_service;
                ALTER DEFAULT PRIVILEGES IN SCHEMA finance GRANT SELECT, INSERT ON TABLES TO finance_service;
                REVOKE UPDATE, DELETE, TRUNCATE ON finance.ledger_entries FROM finance_service;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "fee_structures",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "invoice_items",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "ledger_entries",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payment_transactions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "invoices",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "payments",
                schema: "finance");
        }
    }
}
