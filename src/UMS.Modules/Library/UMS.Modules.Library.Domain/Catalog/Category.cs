using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Library.Domain.Catalog;

/// <summary>
/// requirement-spec.md §3 Module-Local Terms: a lightweight lookup entity referenced by
/// <see cref="Book"/>. <see cref="IsReferenceOnly"/> carries requirement-spec.md §2's "reference-only
/// categories may be excluded from circulation entirely" - §9's own Open Question leaves unresolved
/// "whether reference-only categories need a distinct BookCopy status versus simply never being
/// loan-eligible"; this build takes the latter, simpler reading (a flag gating loan issuance, not a
/// new <see cref="BookCopyStatus"/> value) and documents it as a first-pass choice, not a final
/// data-modeling answer (flagged again in the PR description).
/// </summary>
public sealed class Category
{
    private Category()
    {
    }

    private Category(CategoryId id, string name, bool isReferenceOnly, DateTimeOffset now)
    {
        Id = id;
        Name = name;
        IsReferenceOnly = isReferenceOnly;
        CreatedAt = now;
    }

    public CategoryId Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public bool IsReferenceOnly { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static Result<Category> Create(string name, bool isReferenceOnly, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Error.Validation("category.name_required", "A Category's name is required.");
        }

        return new Category(CategoryId.New(), name.Trim(), isReferenceOnly, now);
    }

    public Result Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure(Error.Validation("category.name_required", "A Category's name is required."));
        }

        Name = name.Trim();
        return Result.Success();
    }

    public void SetReferenceOnly(bool isReferenceOnly) => IsReferenceOnly = isReferenceOnly;
}
