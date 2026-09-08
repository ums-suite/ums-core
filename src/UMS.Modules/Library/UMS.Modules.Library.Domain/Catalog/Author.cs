using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>
/// requirement-spec.md §3 Module-Local Terms: "a lightweight lookup entity referenced by Book, not
/// independently significant to the ubiquitous language" - a plain class, never an
/// <see cref="Common.AggregateRoot{TId}"/> (no domain events, no version-backed transitions of its
/// own - only a name that can be corrected).
/// </summary>
public sealed class Author
{
    private Author()
    {
    }

    private Author(AuthorId id, string name, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        CreatedAt = now;
    }

    public AuthorId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Author> Create(string name, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("author.name_required", "An Author's name is required.");
        }

        return new Author(AuthorId.New(), name.Trim(), now);
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("author.name_required", "An Author's name is required."));
        }

        Name = name.Trim();
        return Result.Success();
    }
}
