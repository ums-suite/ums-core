namespace UMS.Modules.Admission.Application.Results;

public sealed record AdmissionResultDto(Guid Id, Guid CampaignId, Guid MeritListId, string Status, IReadOnlyCollection<AdmissionResultEntryDto> Entries);

public sealed record AdmissionResultEntryDto(Guid ApplicantId, Guid ApplicationId, Guid ProgramId, string Outcome, int MeritRank, int? WaitlistRank);
