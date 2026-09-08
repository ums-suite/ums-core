namespace UMS.Modules.Research.Application.InstitutionalRepositoryEntries;

public sealed record InstitutionalRepositoryEntryDto(
    Guid Id,
    string Title,
    string WorkType,
    ContributorDto Depositor,
    Guid? SupervisingFacultyMemberId,
    DateOnly DepositDate,
    EmbargoPolicyDto Embargo,
    Guid? ArtifactId,
    DateTimeOffset CreatedAt,
    uint Version);

public sealed record ContributorDto(string Name, Guid? FacultyMemberId);

public sealed record EmbargoPolicyDto(bool IsEmbargoed, DateOnly? EmbargoEndDate, string AccessLevel);

public sealed record InstitutionalRepositoryEntryListPage(IReadOnlyList<InstitutionalRepositoryEntryDto> Items, int Skip, int Take);

public sealed record DepositRepositoryEntryRequest(
    string Title,
    string WorkType,
    ContributorDto Depositor,
    Guid? SupervisingFacultyMemberId,
    DateOnly DepositDate,
    EmbargoPolicyDto Embargo);
