using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Research.Domain.Publications;

/// <summary>requirement-spec.md §3: <c>(type: Journal | Conference | BookChapter | Other, name, publisher: nullable)</c>.</summary>
public sealed record Venue
{
    private Venue(VenueType type, string name, string? publisher)
    {
        Type = type;
        Name = name;
        Publisher = publisher;
    }

    public VenueType Type { get; }

    public string Name { get; }

    public string? Publisher { get; }

    public static Result<Venue> Create(VenueType type, string name, string? publisher) =>
        string.IsNullOrWhiteSpace(name)
            ? Error.Validation("venue.name_required", "A Venue's name is required.")
            : new Venue(type, name.Trim(), string.IsNullOrWhiteSpace(publisher) ? null : publisher.Trim());

    /// <summary>For infrastructure round-tripping of an already-validated stored value only.</summary>
    public static Venue FromStoredValue(VenueType type, string name, string? publisher) => new(type, name, publisher);
}
