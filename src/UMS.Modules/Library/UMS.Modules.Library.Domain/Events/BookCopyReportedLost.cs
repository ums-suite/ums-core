using UMS.Modules.Library.Domain.Common;

namespace UMS.Modules.Library.Domain.Events;

/// <summary>LIB-17: raised by <see cref="Catalog.BookCopy.MarkLost"/> - consumers: Reporting, Audit.</summary>
public sealed record BookCopyReportedLost(Guid BookCopyId, Guid BookId, DateTimeOffset OccurredAt) : IDomainEvent;
