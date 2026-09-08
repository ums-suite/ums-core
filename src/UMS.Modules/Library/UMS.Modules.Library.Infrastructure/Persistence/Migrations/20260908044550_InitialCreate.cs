using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UMS.Modules.Library.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "library");

            migrationBuilder.CreateTable(
                name: "authors",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_copies",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accession_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    condition = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    copy_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    lost_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_copies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "book_reservation_supply_flags",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    remaining_copy_count = table.Column<int>(type: "integer", nullable: false),
                    queue_depth = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_event_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_book_reservation_supply_flags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "books",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    isbn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    category_id = table.Column<Guid>(type: "uuid", nullable: true),
                    author_ids = table.Column<string>(type: "jsonb", nullable: false),
                    edition = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_open_access_digital = table.Column<bool>(type: "boolean", nullable: false),
                    withdrawn = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    withdrawn_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_books", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_reference_only = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fine_accruals",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    fine_id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    accrual_date = table.Column<DateOnly>(type: "date", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fine_accruals", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "fines",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    invoice_id = table.Column<Guid>(type: "uuid", nullable: true),
                    waived_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    waived_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    settled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    waived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    amount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_fines", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loan_review_flags",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    source_event_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loan_review_flags", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "loans",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_copy_id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    issued_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issued_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    due_date = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    renewal_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    returned_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    lost_write_off_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loans", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "outbox_messages",
                schema: "library",
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
                name: "overdue_notices",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    loan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    notice_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_overdue_notices", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "processed_inbound_events",
                schema: "library",
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
                name: "reservations",
                schema: "library",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    book_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_id = table.Column<Guid>(type: "uuid", nullable: false),
                    borrower_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    priority = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    offered_copy_id = table.Column<Guid>(type: "uuid", nullable: true),
                    offered_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claim_window_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    claimed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    expired_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_authors_name",
                schema: "library",
                table: "authors",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_book_copies_book_id",
                schema: "library",
                table: "book_copies",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_book_copies_book_id_status",
                schema: "library",
                table: "book_copies",
                columns: new[] { "book_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ux_book_copies_accession_number",
                schema: "library",
                table: "book_copies",
                column: "accession_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_book_reservation_supply_flags_book_id",
                schema: "library",
                table: "book_reservation_supply_flags",
                column: "book_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_category_id",
                schema: "library",
                table: "books",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_books_isbn",
                schema: "library",
                table: "books",
                column: "isbn");

            migrationBuilder.CreateIndex(
                name: "ix_books_title",
                schema: "library",
                table: "books",
                column: "title");

            migrationBuilder.CreateIndex(
                name: "ux_categories_name",
                schema: "library",
                table: "categories",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fine_accruals_fine_id",
                schema: "library",
                table: "fine_accruals",
                column: "fine_id");

            migrationBuilder.CreateIndex(
                name: "ux_fine_accruals_loan_date",
                schema: "library",
                table: "fine_accruals",
                columns: new[] { "loan_id", "accrual_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_fines_borrower_id",
                schema: "library",
                table: "fines",
                column: "borrower_id");

            migrationBuilder.CreateIndex(
                name: "ix_fines_borrower_id_status",
                schema: "library",
                table: "fines",
                columns: new[] { "borrower_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_fines_invoice_id",
                schema: "library",
                table: "fines",
                column: "invoice_id");

            migrationBuilder.CreateIndex(
                name: "ix_fines_loan_id",
                schema: "library",
                table: "fines",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "ix_loan_review_flags_loan_id",
                schema: "library",
                table: "loan_review_flags",
                column: "loan_id");

            migrationBuilder.CreateIndex(
                name: "ix_loans_borrower_id",
                schema: "library",
                table: "loans",
                column: "borrower_id");

            migrationBuilder.CreateIndex(
                name: "ix_loans_status_due_date",
                schema: "library",
                table: "loans",
                columns: new[] { "status", "due_date" });

            migrationBuilder.CreateIndex(
                name: "ux_loans_bookcopy_active",
                schema: "library",
                table: "loans",
                column: "book_copy_id",
                unique: true,
                filter: "status = 'Active'");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_event_type",
                schema: "library",
                table: "outbox_messages",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_processed_at",
                schema: "library",
                table: "outbox_messages",
                column: "processed_at");

            migrationBuilder.CreateIndex(
                name: "ux_overdue_notices_loan_date",
                schema: "library",
                table: "overdue_notices",
                columns: new[] { "loan_id", "notice_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_reservations_book_status_priority_created",
                schema: "library",
                table: "reservations",
                columns: new[] { "book_id", "status", "priority", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_reservations_borrower_id",
                schema: "library",
                table: "reservations",
                column: "borrower_id");

            migrationBuilder.CreateIndex(
                name: "ix_reservations_offered_copy_id",
                schema: "library",
                table: "reservations",
                column: "offered_copy_id");

            migrationBuilder.CreateIndex(
                name: "ux_reservations_book_borrower_open",
                schema: "library",
                table: "reservations",
                columns: new[] { "book_id", "borrower_id" },
                unique: true,
                filter: "status IN ('Queued', 'Offered')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authors",
                schema: "library");

            migrationBuilder.DropTable(
                name: "book_copies",
                schema: "library");

            migrationBuilder.DropTable(
                name: "book_reservation_supply_flags",
                schema: "library");

            migrationBuilder.DropTable(
                name: "books",
                schema: "library");

            migrationBuilder.DropTable(
                name: "categories",
                schema: "library");

            migrationBuilder.DropTable(
                name: "fine_accruals",
                schema: "library");

            migrationBuilder.DropTable(
                name: "fines",
                schema: "library");

            migrationBuilder.DropTable(
                name: "loan_review_flags",
                schema: "library");

            migrationBuilder.DropTable(
                name: "loans",
                schema: "library");

            migrationBuilder.DropTable(
                name: "outbox_messages",
                schema: "library");

            migrationBuilder.DropTable(
                name: "overdue_notices",
                schema: "library");

            migrationBuilder.DropTable(
                name: "processed_inbound_events",
                schema: "library");

            migrationBuilder.DropTable(
                name: "reservations",
                schema: "library");
        }
    }
}
