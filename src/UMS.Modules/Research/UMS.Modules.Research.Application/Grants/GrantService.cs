using System.Text.Json;
using UMS.Modules.Research.Application.Abstractions;
using UMS.Modules.Research.Application.Common;
using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.Grants;
using UMS.Shared.Audit;
using UMS.Shared.Domain;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Faculty;

namespace UMS.Modules.Research.Application.Grants;

/// <summary>
/// RES-2/RES-3/RES-4: the Grant lifecycle + investigator-management application service
/// (requirement-spec.md §2 Grant Lifecycle, §6 API Surface Grant rows). Ownership ("PI or
/// Admin/Research-Office") is resolved by the CALLING Api layer (mirrors Library's own
/// <c>OwnershipGuard</c> pattern) - every method here assumes the caller has already been
/// authorized, and only enforces the domain's own invariants (PI-vacancy guard, optimistic
/// concurrency, forward-only lifecycle).
/// </summary>
public sealed class GrantService(
    IGrantRepository grants,
    IFundingBodyRepository fundingBodies,
    IFacultyMemberLookup facultyMemberLookup,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IClock clock)
{
    public static GrantDto ToDto(Grant grant) => new(
        grant.Id.Value,
        grant.Title,
        grant.Description,
        grant.FundingBodyId,
        grant.FundingAmount.Amount,
        grant.FundingAmount.Currency,
        grant.FundingPeriod.Start,
        grant.FundingPeriod.End,
        grant.Status.ToString(),
        grant.RequiresPiReassignment,
        grant.IsPubliclyVisible,
        grant.AwardDate,
        grant.PrincipalInvestigatorFacultyMemberId,
        grant.Investigators.Select(i => new GrantInvestigatorDto(i.FacultyMemberId, i.Role.ToString(), i.AddedAt)).OrderBy(i => i.AddedAt).ToList(),
        grant.Version);

    public async Task<Result<GrantDto>> ProposeAsync(ProposeGrantRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var fundingBody = await fundingBodies.GetByIdAsync(new Domain.FundingBodies.FundingBodyId(request.FundingBodyId), cancellationToken).ConfigureAwait(false);
        if (fundingBody is null)
        {
            return Error.NotFound("fundingbody.not_found", $"No FundingBody exists with id '{request.FundingBodyId}'.");
        }

        var piValidation = await ValidateFacultyMemberAsync(request.PrincipalInvestigatorFacultyMemberId, cancellationToken).ConfigureAwait(false);
        if (piValidation.IsFailure)
        {
            return piValidation.Error!;
        }

        var amount = Money.Create(request.FundingAmount, request.Currency);
        if (amount.IsFailure)
        {
            return amount.Error!;
        }

        var period = DateRange.Create(request.FundingPeriodStart, request.FundingPeriodEnd);
        if (period.IsFailure)
        {
            return period.Error!;
        }

        Grant grant;
        try
        {
            grant = Grant.Propose(request.Title, request.Description, request.FundingBodyId, amount.Value, period.Value, request.PrincipalInvestigatorFacultyMemberId, clock.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("grant.invalid", ex.Message);
        }

        grants.Add(grant);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Grant", grant.Id.Value.ToString(), AuditActions.Create, null, JsonSerializer.Serialize(new { status = grant.Status.ToString(), fundingBodyId = grant.FundingBodyId }));
        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(grant);
    }

    public async Task<Result<GrantDto>> FundAsync(Guid id, FundGrantRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        var amount = Money.Create(request.ConfirmedFundingAmount, request.ConfirmedCurrency);
        if (amount.IsFailure)
        {
            return amount.Error!;
        }

        var period = DateRange.Create(request.ConfirmedFundingPeriodStart, request.ConfirmedFundingPeriodEnd);
        if (period.IsFailure)
        {
            return period.Error!;
        }

        return await LoadAndTransitionAsync(id, request.Version, audit, "fund", grant => grant.Fund(request.AwardDate, amount.Value, period.Value, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<GrantDto>> ActivateAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        LoadAndTransitionAsync(id, expectedVersion, audit, "activate", grant => grant.Activate(clock.UtcNow), cancellationToken);

    public Task<Result<GrantDto>> CloseAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        LoadAndTransitionAsync(id, expectedVersion, audit, "close", grant => grant.Close(clock.UtcNow), cancellationToken);

    public async Task<Result<GrantDto>> ReportAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        await LoadAndTransitionAsync(id, expectedVersion, audit, "report", grant => grant.Report(clock.UtcNow), cancellationToken).ConfigureAwait(false);

    public Task<Result<GrantDto>> RejectAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        LoadAndTransitionAsync(id, expectedVersion, audit, "reject", grant => grant.Reject(clock.UtcNow), cancellationToken);

    public Task<Result<GrantDto>> WithdrawAsync(Guid id, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        LoadAndTransitionAsync(id, expectedVersion, audit, "withdraw", grant => grant.Withdraw(clock.UtcNow), cancellationToken);

    /// <summary>RES-3: add a Co-Investigator, or reassign the PI (promoting an existing Co-Investigator or naming a new one) - see <see cref="Grant.AddInvestigator"/>'s own remarks for the reassignment mechanics.</summary>
    public async Task<Result<GrantDto>> AddInvestigatorAsync(Guid id, AddGrantInvestigatorRequest request, AuditContext audit, CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<GrantInvestigatorRole>(request.Role, ignoreCase: true, out var role))
        {
            return Error.Validation("grant.invalid_role", $"'{request.Role}' is not a recognized GrantInvestigatorRole.");
        }

        var facultyValidation = await ValidateFacultyMemberAsync(request.FacultyMemberId, cancellationToken).ConfigureAwait(false);
        if (facultyValidation.IsFailure)
        {
            return facultyValidation.Error!;
        }

        return await LoadAndTransitionAsync(id, request.Version, audit, "add_investigator", grant => grant.AddInvestigator(request.FacultyMemberId, role, clock.UtcNow), cancellationToken).ConfigureAwait(false);
    }

    public Task<Result<GrantDto>> RemoveInvestigatorAsync(Guid id, Guid facultyMemberId, uint expectedVersion, AuditContext audit, CancellationToken cancellationToken = default) =>
        LoadAndTransitionAsync(id, expectedVersion, audit, "remove_investigator", grant => grant.RemoveInvestigator(facultyMemberId), cancellationToken);

    public async Task<Result<GrantDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var grant = await grants.GetByIdAsync(new GrantId(id), cancellationToken).ConfigureAwait(false);
        return grant is null ? Error.NotFound("grant.not_found", $"No Grant exists with id '{id}'.") : ToDto(grant);
    }

    public async Task<GrantListPage> ListAsync(Guid? principalInvestigatorFacultyMemberId, Guid? facultyMemberId, string? status, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        GrantStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<GrantStatus>(status, ignoreCase: true, out var s))
        {
            parsedStatus = s;
        }

        var items = await grants.ListAsync(principalInvestigatorFacultyMemberId, facultyMemberId, parsedStatus, skip, take, cancellationToken).ConfigureAwait(false);
        return new GrantListPage(items.Select(ToDto).ToList(), skip, take);
    }

    public async Task<GrantListPage> ListPubliclyVisibleAsync(int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take <= 0 ? 50 : take, 1, 200);

        var items = await grants.ListPubliclyVisibleAsync(skip, take, cancellationToken).ConfigureAwait(false);
        return new GrantListPage(items.Select(ToDto).ToList(), skip, take);
    }

    public async Task<Result<GrantDto>> SetPubliclyVisibleAsync(Guid id, bool isPubliclyVisible, CancellationToken cancellationToken = default)
    {
        var grant = await grants.GetByIdAsync(new GrantId(id), cancellationToken).ConfigureAwait(false);
        if (grant is null)
        {
            return Error.NotFound("grant.not_found", $"No Grant exists with id '{id}'.");
        }

        grant.SetPubliclyVisible(isPubliclyVisible);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(grant);
    }

    private async Task<Result> ValidateFacultyMemberAsync(Guid facultyMemberId, CancellationToken cancellationToken)
    {
        // requirement-spec.md item 13: every FacultyMemberId reference is validated against
        // UMS.Shared.Faculty.IFacultyMemberLookup at write time - Research never assumes a
        // caller-supplied FacultyMemberId is real.
        var facultyMember = await facultyMemberLookup.GetAsync(facultyMemberId, cancellationToken).ConfigureAwait(false);
        return facultyMember is null
            ? Result.Failure(Error.NotFound("facultymember.not_found", $"No FacultyMember exists with id '{facultyMemberId}'."))
            : Result.Success();
    }

    private async Task<Result<GrantDto>> LoadAndTransitionAsync(Guid id, uint expectedVersion, AuditContext audit, string action, Action<Grant> transition, CancellationToken cancellationToken)
    {
        var grant = await grants.GetByIdAsync(new GrantId(id), cancellationToken).ConfigureAwait(false);
        if (grant is null)
        {
            return Error.NotFound("grant.not_found", $"No Grant exists with id '{id}'.");
        }

        var beforeStatus = grant.Status.ToString();
        var beforeRequiresPiReassignment = grant.RequiresPiReassignment;
        var beforeInvestigatorCount = grant.Investigators.Count;

        try
        {
            transition(grant);
        }
        catch (InvalidOperationException ex)
        {
            return Error.Conflict("grant.invalid_transition", ex.Message);
        }

        unitOfWork.SetExpectedVersion(grant, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest(
            "Grant",
            grant.Id.Value.ToString(),
            action,
            JsonSerializer.Serialize(new { status = beforeStatus, requiresPiReassignment = beforeRequiresPiReassignment, investigatorCount = beforeInvestigatorCount }),
            JsonSerializer.Serialize(new { status = grant.Status.ToString(), requiresPiReassignment = grant.RequiresPiReassignment, investigatorCount = grant.Investigators.Count }));

        var commitResult = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return commitResult.IsFailure ? commitResult.Error! : ToDto(grant);
    }
}
