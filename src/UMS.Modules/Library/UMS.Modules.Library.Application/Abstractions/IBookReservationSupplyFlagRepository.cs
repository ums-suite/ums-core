using UMS.Modules.Library.Domain.Reservations;

namespace UMS.Modules.Library.Application.Abstractions;

public interface IBookReservationSupplyFlagRepository
{
    public Task<IReadOnlyList<BookReservationSupplyFlag>> GetByBookAsync(Guid bookId, CancellationToken cancellationToken = default);

    public void Add(BookReservationSupplyFlag flag);
}
