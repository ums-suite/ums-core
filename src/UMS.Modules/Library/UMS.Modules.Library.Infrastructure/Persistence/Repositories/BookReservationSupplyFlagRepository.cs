using Microsoft.EntityFrameworkCore;
using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Infrastructure.Persistence.Repositories;

internal sealed class BookReservationSupplyFlagRepository(LibraryDbContext context) : IBookReservationSupplyFlagRepository
{
    public async Task<IReadOnlyList<BookReservationSupplyFlag>> GetByBookAsync(Guid bookId, CancellationToken cancellationToken = default) =>
        await context.BookReservationSupplyFlags.Where(f => f.BookId == bookId).OrderByDescending(f => f.CreatedAt).ToListAsync(cancellationToken).ConfigureAwait(false);

    public void Add(BookReservationSupplyFlag flag) => context.BookReservationSupplyFlags.Add(flag);
}
