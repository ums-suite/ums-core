namespace UMS.Modules.Admission.Application.MeritLists;

public sealed record MeritListDto(Guid Id, Guid CampaignId, string Status, IReadOnlyCollection<MeritListEntryDto> Entries);

public sealed record MeritListEntryDto(Guid ApplicantId, Guid ApplicationId, Guid ProgramId, decimal Score, int Rank, string Outcome, int? WaitlistRank);
