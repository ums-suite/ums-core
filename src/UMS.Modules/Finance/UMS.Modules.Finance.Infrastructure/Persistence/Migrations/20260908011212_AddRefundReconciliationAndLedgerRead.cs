using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace UMS.Modules.Finance.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRefundReconciliationAndLedgerRead : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reconciliation_exceptions",
                schema: "finance",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    settlement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    expected_gateway_transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    actual_gateway_transaction_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    actual_gateway_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    details = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reconciliation_exceptions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "refunds",
                schema: "finance",
                columns: table => new
                {
                    payment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    payment_transaction_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    method = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    requested_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    gateway_refund_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refunds", x => new { x.payment_id, x.ordinal });
                    table.ForeignKey(
                        name: "FK_refunds_payments_payment_id",
                        column: x => x.payment_id,
                        principalSchema: "finance",
                        principalTable: "payments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_exceptions_payment_id",
                schema: "finance",
                table: "reconciliation_exceptions",
                column: "payment_id");

            migrationBuilder.CreateIndex(
                name: "ix_reconciliation_exceptions_settlement_date",
                schema: "finance",
                table: "reconciliation_exceptions",
                column: "settlement_date");

            migrationBuilder.CreateIndex(
                name: "ux_refunds_id",
                schema: "finance",
                table: "refunds",
                column: "id",
                unique: true);

            // FIN-14: reconciliation_exceptions is append-only, the identical posture
            // InitialCreate's own migration already locks down for ledger_entries (requirement-spec.md
            // §4's discipline, mirrored here for its own module-local ReconciliationException term) -
            // same known gap that migration documents: load-bearing only once the runtime connects as
            // a genuine non-owner role, not the schema owner it connects as today.
            migrationBuilder.Sql(
                """
                REVOKE UPDATE, DELETE, TRUNCATE ON finance.reconciliation_exceptions FROM finance_service;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reconciliation_exceptions",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "refunds",
                schema: "finance");
        }
    }
}
