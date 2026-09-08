using UMS.Modules.Library.Application.Abstractions;
using UMS.Modules.Library.Domain.Catalog;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Application.Catalog;

/// <summary>LIB-1: requirement-spec.md §2 Catalog Management - CRUD for Book/BookCopy/Author/Category.</summary>
public sealed class CatalogService(
    IBookRepository books,
    IAuthorRepository authors,
    ICategoryRepository categories,
    IBookCopyRepository bookCopies,
    IUnitOfWork unitOfWork,
    IClock clock)
{
    public async Task<Result<BookDto>> CreateBookAsync(CreateBookRequest request, CancellationToken cancellationToken = default)
    {
        var created = Book.Create(request.Title, request.Isbn, request.CategoryId is { } categoryId ? new CategoryId(categoryId) : null, request.AuthorIds, request.Edition, request.IsOpenAccessDigital, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        books.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    public async Task<Result<BookDto>> UpdateBookAsync(Guid id, UpdateBookRequest request, CancellationToken cancellationToken = default)
    {
        var book = await books.GetByIdAsync(new BookId(id), cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return Error.NotFound("book.not_found", $"No Book exists with id '{id}'.");
        }

        var updated = book.UpdateDetails(request.Title, request.Isbn, request.CategoryId is { } categoryId ? new CategoryId(categoryId) : null, request.AuthorIds, request.Edition);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(book);
    }

    public async Task<Result<BookDto>> GetBookByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await books.GetByIdAsync(new BookId(id), cancellationToken).ConfigureAwait(false);
        return book is null ? Error.NotFound("book.not_found", $"No Book exists with id '{id}'.") : ToDto(book);
    }

    /// <summary>requirement-spec.md §8: "existing loans are unaffected; no new Loan or Reservation may be created against it."</summary>
    public async Task<Result<BookDto>> WithdrawBookAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var book = await books.GetByIdAsync(new BookId(id), cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return Error.NotFound("book.not_found", $"No Book exists with id '{id}'.");
        }

        var withdrawn = book.Withdraw(clock.UtcNow);
        if (withdrawn.IsFailure)
        {
            return withdrawn.Error!;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(book);
    }

    public async Task<Result<AuthorDto>> CreateAuthorAsync(CreateAuthorRequest request, CancellationToken cancellationToken = default)
    {
        var created = Author.Create(request.Name, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        authors.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AuthorDto(created.Value.Id.Value, created.Value.Name);
    }

    public async Task<IReadOnlyList<AuthorDto>> SearchAuthorsAsync(string? searchText, CancellationToken cancellationToken = default) =>
        (await authors.SearchByNameAsync(searchText, take: 25, cancellationToken).ConfigureAwait(false))
            .Select(a => new AuthorDto(a.Id.Value, a.Name))
            .ToList();

    public async Task<Result<CategoryDto>> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var created = Category.Create(request.Name, request.IsReferenceOnly, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        categories.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new CategoryDto(created.Value.Id.Value, created.Value.Name, created.Value.IsReferenceOnly);
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllCategoriesAsync(CancellationToken cancellationToken = default) =>
        (await categories.GetAllAsync(cancellationToken).ConfigureAwait(false))
            .Select(c => new CategoryDto(c.Id.Value, c.Name, c.IsReferenceOnly))
            .ToList();

    public async Task<Result<BookCopyDto>> CreateBookCopyAsync(CreateBookCopyRequest request, CancellationToken cancellationToken = default)
    {
        var book = await books.GetByIdAsync(new BookId(request.BookId), cancellationToken).ConfigureAwait(false);
        if (book is null)
        {
            return Error.NotFound("book.not_found", $"No Book exists with id '{request.BookId}'.");
        }

        if (!Enum.TryParse<CopyType>(request.CopyType, ignoreCase: true, out var copyType))
        {
            return Error.Validation("book_copy.invalid_copy_type", $"'{request.CopyType}' is not a recognized copy type.");
        }

        var created = BookCopy.Create(book.Id, request.AccessionNumber, request.Condition ?? "Good", copyType, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        bookCopies.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value);
    }

    /// <summary>LIB-3: <c>GET /books/{id}/copies</c> - copy-level availability for a title.</summary>
    public async Task<IReadOnlyList<BookCopyDto>> GetCopiesByBookAsync(Guid bookId, CancellationToken cancellationToken = default) =>
        (await bookCopies.GetByBookAsync(new BookId(bookId), cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    internal static BookDto ToDto(Book book) => new(book.Id.Value, book.Title, book.Isbn, book.CategoryId?.Value, book.AuthorIds.ToList(), book.Edition, book.IsOpenAccessDigital, book.Withdrawn, book.CreatedAt);

    internal static BookCopyDto ToDto(BookCopy copy) => new(copy.Id.Value, copy.BookId.Value, copy.AccessionNumber, copy.Condition, copy.CopyType.ToString(), copy.Status.ToString(), copy.CreatedAt);
}
