namespace UMS.Modules.Library.Application.Catalog;

public sealed record BookDto(Guid Id, string Title, string? Isbn, Guid? CategoryId, IReadOnlyList<Guid> AuthorIds, string? Edition, bool IsOpenAccessDigital, bool Withdrawn, DateTimeOffset CreatedAt);

public sealed record AuthorDto(Guid Id, string Name);

public sealed record CategoryDto(Guid Id, string Name, bool IsReferenceOnly);

public sealed record BookCopyDto(Guid Id, Guid BookId, string AccessionNumber, string Condition, string CopyType, string Status, DateTimeOffset CreatedAt);

public sealed record CreateBookRequest(string Title, string? Isbn, Guid? CategoryId, IReadOnlyList<Guid>? AuthorIds, string? Edition, bool IsOpenAccessDigital);

public sealed record UpdateBookRequest(string Title, string? Isbn, Guid? CategoryId, IReadOnlyList<Guid>? AuthorIds, string? Edition);

public sealed record CreateAuthorRequest(string Name);

public sealed record CreateCategoryRequest(string Name, bool IsReferenceOnly);

public sealed record CreateBookCopyRequest(Guid BookId, string AccessionNumber, string? Condition, string CopyType);
