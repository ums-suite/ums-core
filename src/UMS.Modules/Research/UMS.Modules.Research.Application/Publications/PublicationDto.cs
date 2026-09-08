namespace UMS.Modules.Research.Application.Publications;

public sealed record PublicationDto(
    Guid Id,
    string Title,
    IReadOnlyList<AuthorEntryDto> Authors,
    VenueDto Venue,
    CitationMetadataDto Citation,
    IReadOnlyList<Guid> FundedByGrantIds,
    bool IsPubliclyVisible,
    Guid? MergedIntoPublicationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    uint Version);

public sealed record AuthorEntryDto(int Order, Guid? FacultyMemberId, string Name, string? Affiliation, bool IsCorrespondingAuthor);

public sealed record VenueDto(string Type, string Name, string? Publisher);

public sealed record CitationMetadataDto(string? Doi, DateOnly PublicationDate, int? CitationCount);

public sealed record PublicationListPage(IReadOnlyList<PublicationDto> Items, int Skip, int Take);

public sealed record CreatePublicationRequest(string Title, IReadOnlyList<AuthorEntryDto> Authors, VenueDto Venue, CitationMetadataDto Citation);

public sealed record UpdatePublicationRequest(string Title, IReadOnlyList<AuthorEntryDto> Authors, VenueDto Venue, CitationMetadataDto Citation, uint Version);

public sealed record MergePublicationsRequest(Guid MergedPublicationId);
