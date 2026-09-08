using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class ReservationRepository(LibraryDbContext context) : IReservationRepository
{
    private static readonly ReservationStatus[] OpenStatuses = [ReservationStatus.Queued, ReservationStatus.Offered];

    public Task<Reservation?> GetByIdAsync(ReservationId id, CancellationToken cancellationToken = default) =>
        context.Reservations.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Reservation>> GetByBorrowerAsync(Guid borrowerId, CancellationToken cancellationToken = default) =>
        await context.Reservations.Where(r => r.BorrowerId == borrowerId).OrderByDescending(r => r.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public Task<bool> HasOpenReservationAsync(Guid bookId, Guid borrowerId, CancellationToken cancellationToken = default) =>
        context.Reservations.AnyAsync(r => r.BookId == bookId && r.BorrowerId == borrowerId && OpenStatuses.Contains(r.Status), cancellationToken);

    public Task<int> CountOpenByBookAsync(Guid bookId, CancellationToken cancellationToken = default) =>
        context.Reservations.CountAsync(r => r.BookId == bookId && OpenStatuses.Contains(r.Status), cancellationToken);

    /// <summary>
    /// design-decisions.md "Reservation-Queue Fairness and Claim Mechanism": one atomic conditional
    /// UPDATE selecting the next Queued Reservation by fairness order (Priority, then CreatedAt) and
    /// offering it the just-freed copy - a second concurrent invocation for the same event finds no
    /// remaining Queued row to select (or the caller's own BookCopy-row lock already serializes it),
    /// so this is race-free by construction, not by an additional application-level check.
    /// </summary>
    public async Task<Guid?> TryOfferNextQueuedAsync(Guid bookId, Guid bookCopyId, DateTimeOffset offeredAt, DateTimeOffset claimWindowExpiresAt, CancellationToken cancellationToken = default)
    {
        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        }

        var transaction = context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;

        await using var command = new NpgsqlCommand(
            """
            UPDATE library.reservations
            SET status = 'Offered', offered_copy_id = @bookCopyId, offered_at = @offeredAt, claim_window_expires_at = @claimWindowExpiresAt
            WHERE id = (
                SELECT id FROM library.reservations
                WHERE book_id = @bookId AND status = 'Queued'
                ORDER BY priority ASC, created_at ASC
                LIMIT 1
                FOR UPDATE SKIP LOCKED)
            RETURNING id
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("bookId", bookId);
        command.Parameters.AddWithValue("bookCopyId", bookCopyId);
        command.Parameters.AddWithValue("offeredAt", offeredAt);
        command.Parameters.AddWithValue("claimWindowExpiresAt", claimWindowExpiresAt);

        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is Guid id ? id : null;
    }

    public Task<Reservation?> GetOfferedByCopyForUpdateAsync(Guid bookCopyId, CancellationToken cancellationToken = default) =>
        context.Reservations
            .FromSqlInterpolated($"SELECT *, xmin FROM library.reservations WHERE offered_copy_id = {bookCopyId} AND status = 'Offered' FOR UPDATE")
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<Reservation>> GetOfferedPastClaimWindowAsync(DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await context.Reservations
            .Where(r => r.Status == ReservationStatus.Offered && r.ClaimWindowExpiresAt != null && r.ClaimWindowExpiresAt < asOf)
            .OrderBy(r => r.ClaimWindowExpiresAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    public void Add(Reservation reservation) => context.Reservations.Add(reservation);
}
