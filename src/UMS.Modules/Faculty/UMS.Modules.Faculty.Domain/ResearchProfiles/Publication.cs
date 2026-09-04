namespace UMS.Modules.Faculty.Domain.ResearchProfiles;

/// <summary>
/// requirement-spec.md faculty §2 Research Profile, §8: "entry with an unverified/self-reported
/// publication ... published to the Public Website as-is; no verification workflow is specified"
/// - accepted as-is, a first-pass gap rather than something this value object silently designs
/// around.
/// </summary>
public sealed class Publication
{
    private Publication()
    {
    }

    private Publication(string title, string venue, int year, string? url)
    {
        Title = title;
        Venue = venue;
        Year = year;
        Url = url;
    }

    public string Title { get; private set; } = string.Empty;

    public string Venue { get; private set; } = string.Empty;

    public int Year { get; private set; }

    public string? Url { get; private set; }

    public static Publication Create(string title, string venue, int year, string? url)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Publication title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(venue))
        {
            throw new ArgumentException("Publication venue is required.", nameof(venue));
        }

        return new Publication(title.Trim(), venue.Trim(), year, string.IsNullOrWhiteSpace(url) ? null : url.Trim());
    }
}
