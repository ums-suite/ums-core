using UMS.Modules.Hostel.Application.Abstractions;
using UMS.Modules.Hostel.Application.Common;
using UMS.Modules.Hostel.Domain.Applications;
using UMS.Modules.Hostel.Domain.ApplicationWindows;
using UMS.Modules.Hostel.Domain.Hostels;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Hostel.Application.Applications;

/// <summary>
/// HOS-3: <c>POST /applications</c>/<c>GET /applications/me</c> (requirement-spec.md §2 step 2, §6),
/// plus the HOS-withdrawal gap-fill (design-decisions.md "HostelApplication Withdrawal as a
/// First-Class State Transition").
/// </summary>
public sealed class HostelApplicationService(
    IHostelApplicationRepository applications,
    IApplicationWindowRepository windows,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    /// <summary>requirement-spec.md §2 step 2; §8 edge cases "window closed -&gt; rejected with machine-readable error", "existing active Allocation applies again -&gt; 409".</summary>
    public async Task<Result<HostelApplicationDto>> SubmitNewApplicationAsync(Guid studentId, CreateHostelApplicationRequest request, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var window = await windows.GetByIdAsync(new ApplicationWindowId(request.ApplicationWindowId), cancellationToken).ConfigureAwait(false);
        if (window is null)
        {
            return Error.NotFound("application_window.not_found", $"No ApplicationWindow exists with id '{request.ApplicationWindowId}'.");
        }

        if (!window.IsOpenAt(now))
        {
            return Error.Conflict("hostel_application.window_closed", $"ApplicationWindow '{window.Id}' is not open (opens {window.OpensAt:O}, closes {window.ClosesAt:O}).");
        }

        if (await applications.StudentHasActiveAllocationAsync(studentId, cancellationToken).ConfigureAwait(false))
        {
            return Error.Conflict("hostel_application.already_allocated", $"Student '{studentId}' already has an active Allocation and cannot submit a new HostelApplication.");
        }

        var existing = await applications.GetActiveByStudentAndWindowAsync(studentId, request.ApplicationWindowId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return Error.Conflict("hostel_application.already_open", $"Student '{studentId}' already has an open HostelApplication for this ApplicationWindow.");
        }

        var created = HostelApplication.CreateDraft(studentId, request.ApplicationWindowId, request.YearOfStudy, request.HasFinancialNeed, request.HomeDistrictDistanceKm, now);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        var application = created.Value;

        var preferences = new List<HostelPreference>();
        foreach (var p in request.Preferences)
        {
            if (!Enum.TryParse<RoomType>(p.PreferredRoomType, ignoreCase: true, out var roomType))
            {
                return Error.Validation("hostel_preference.invalid_room_type", $"'{p.PreferredRoomType}' is not a recognized Room type.");
            }

            var preference = HostelPreference.Create(p.HostelId, roomType, p.Rank);
            if (preference.IsFailure)
            {
                return preference.Error!;
            }

            preferences.Add(preference.Value);
        }

        var replaced = application.ReplacePreferences(preferences);
        if (replaced.IsFailure)
        {
            return replaced.Error!;
        }

        var submitted = application.Submit(now);
        if (submitted.IsFailure)
        {
            return submitted.Error!;
        }

        applications.Add(application);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(application);
    }

    public async Task<Result<HostelApplicationDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var application = await applications.GetByIdAsync(new HostelApplicationId(id), cancellationToken).ConfigureAwait(false);
        return application is null
            ? Error.NotFound("hostel_application.not_found", $"No HostelApplication exists with id '{id}'.")
            : ToDto(application);
    }

    public async Task<IReadOnlyList<HostelApplicationDto>> GetByStudentAsync(Guid studentId, CancellationToken cancellationToken = default) =>
        (await applications.GetByStudentAsync(studentId, cancellationToken).ConfigureAwait(false)).Select(ToDto).ToList();

    /// <summary>
    /// edge-cases.md "HostelApplication withdrawn while its allocation is mid-approval": takes the
    /// SAME <c>SELECT ... FOR UPDATE</c> lock the review-approval command takes
    /// (<c>AllocationService.ApproveAndAllocateAsync</c>), so the two transitions serialize.
    /// </summary>
    public async Task<Result> WithdrawAsync(Guid id, Guid actorUserId, string correlationId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        var application = await applications.GetByIdForUpdateAsync(new HostelApplicationId(id), cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return Result.Failure(Error.NotFound("hostel_application.not_found", $"No HostelApplication exists with id '{id}'."));
        }

        var statusBefore = application.Status;
        var withdrawn = application.Withdraw(clock.UtcNow);
        if (withdrawn.IsFailure)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return withdrawn;
        }

        var audit = new AuditContext(actorUserId, ActorIpAddress: null, correlationId)
            .ToRequest("HostelApplication", application.Id.Value.ToString(), AuditActions.Update, $"{{\"status\":\"{statusBefore}\"}}", "{\"status\":\"Withdrawn\"}");

        return await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, audit, cancellationToken).ConfigureAwait(false);
    }

    internal static HostelApplicationDto ToDto(HostelApplication application) => new(
        application.Id.Value,
        application.StudentId,
        application.ApplicationWindowId,
        application.Status.ToString(),
        application.Preferences.Select(p => new HostelPreferenceDto(p.HostelId, p.PreferredRoomType.ToString(), p.Rank)).ToList(),
        application.YearOfStudy,
        application.HasFinancialNeed,
        application.HomeDistrictDistanceKm,
        application.EligibilityScore,
        application.IsEligible,
        application.RankPosition,
        application.DecisionReason,
        application.AllocationId,
        application.CreatedAt,
        application.SubmittedAt);
}
