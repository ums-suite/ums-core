using System.Text.Json;
using UMS.Modules.Faculty.Application.Abstractions;
using UMS.Modules.Faculty.Application.Common;
using UMS.Modules.Faculty.Domain.FacultyMembers;
using UMS.Modules.Faculty.Domain.ResearchProfiles;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Faculty.Application.ResearchProfiles;

/// <summary>FAC-13: ResearchProfile create-on-first-write + update (requirement-spec.md faculty §2 Research Profile, §6). Owning FacultyMember or HR/Registrar may write (§6 `PUT` row).</summary>
public sealed class ResearchProfileService(
    IResearchProfileRepository researchProfiles,
    IFacultyMemberRepository facultyMembers,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static ResearchProfileDto ToDto(ResearchProfile profile) => new(
        profile.Id.Value,
        profile.FacultyMemberId,
        profile.Publications.Select(p => new PublicationDto(p.Title, p.Venue, p.Year, p.Url)).ToList(),
        profile.OngoingResearch,
        profile.Grants,
        profile.Version);

    public async Task<Result<ResearchProfileDto>> GetByFacultyMemberIdAsync(Guid facultyMemberId, CancellationToken cancellationToken = default)
    {
        var profile = await researchProfiles.GetByFacultyMemberIdAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        return profile is null
            ? Error.NotFound("researchprofile.not_found", $"No ResearchProfile exists for FacultyMember '{facultyMemberId}'.")
            : ToDto(profile);
    }

    /// <summary>Callers holding <c>faculty.research.update</c> may write any FacultyMember's profile (HR); the owning FacultyMember may always write their own - enforced by the caller (endpoint), which passes <paramref name="isPrivileged"/>.</summary>
    public async Task<Result<ResearchProfileDto>> UpdateAsync(Guid facultyMemberId, Guid callerUserId, bool isPrivileged, UpdateResearchProfileRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var facultyMember = await facultyMembers.GetByIdAsync(new FacultyMemberId(facultyMemberId), cancellationToken).ConfigureAwait(false);
        if (facultyMember is null)
        {
            return Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{facultyMemberId}'.");
        }

        if (!isPrivileged && facultyMember.UserId != callerUserId)
        {
            return Error.Forbidden("researchprofile.not_owner", "Only the owning FacultyMember or HR may update this ResearchProfile.");
        }

        List<Publication> publications;
        try
        {
            publications = request.Publications.Select(p => Publication.Create(p.Title, p.Venue, p.Year, p.Url)).ToList();
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("researchprofile.invalid_publication", ex.Message);
        }

        var now = clock.UtcNow;
        var profile = await researchProfiles.GetByFacultyMemberIdAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        var isNew = profile is null;
        if (profile is null)
        {
            profile = ResearchProfile.Create(facultyMemberId, now);
            researchProfiles.Add(profile);
        }
        else
        {
            unitOfWork.SetExpectedVersion(profile, request.Version);
        }

        profile.Update(publications, request.OngoingResearch, request.Grants, now);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "ResearchProfile",
            profile.Id.Value.ToString(),
            isNew ? AuditActions.Create : AuditActions.Publish,
            null,
            JsonSerializer.Serialize(new { publicationCount = publications.Count }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(profile);
    }
}
