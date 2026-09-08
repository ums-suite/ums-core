using UMS.Modules.Research.Domain.Common;
using UMS.Modules.Research.Domain.Events;
using UMS.Shared.Domain;

namespace UMS.Modules.Research.Domain.Grants;

/// <summary>
/// RES-2/RES-3/RES-4/RES-5: the Grant aggregate root (requirement-spec.md §2 Grant Lifecycle, §3,
/// §4). Lifecycle: <c>Proposed -&gt; Funded -&gt; Active -&gt; Closed -&gt; Reported</c>, with
/// <c>Rejected</c> reachable only from <c>Proposed</c> and <c>Withdrawn</c> reachable from
/// <c>Proposed</c>/<c>Funded</c>/<c>Active</c> - both terminal, forward-only, no state skipped.
///
/// <para>
/// design-decisions.md "Grant Lifecycle State Machine and PI-Vacancy Handling": <see
/// cref="RequiresPiReassignment"/> is a guard flag set by consuming Faculty's own
/// <c>FacultyMemberStatusChanged</c> event for any FacultyMember holding an active
/// <see cref="GrantInvestigatorRole.PrincipalInvestigator"/> role on a <c>Funded</c>/<c>Active</c>
/// Grant. While set, every forward lifecycle transition (<see cref="Fund"/>/<see cref="Activate"/>/
/// <see cref="Close"/>/<see cref="Report"/>) is blocked - but <see cref="Withdraw"/> and investigator
/// mutations (<see cref="AddInvestigator"/>/<see cref="RemoveInvestigator"/>, the only ways to
/// actually resolve the vacancy) remain permitted. Never auto-closed, never auto-promoted.
/// </para>
///
/// <para>
/// design-decisions.md "Grant and Investigator-List Concurrency Control": every state-changing
/// method here is meant to be called under an optimistic <c>xmin</c> version check
/// (<c>IUnitOfWork.SetExpectedVersion</c>) applied uniformly by the caller - lifecycle transitions
/// and investigator add/remove alike race the same aggregate instance, mirroring Faculty's own
/// <c>LeaveRequest</c> exactly.
/// </para>
/// </summary>
public sealed class Grant : AggregateRoot<GrantId>
{
    private readonly List<GrantInvestigator> _investigators = [];

    private Grant()
    {
    }

    private Grant(GrantId id, string title, string description, Guid fundingBodyId, Money fundingAmount, DateRange fundingPeriod, Guid principalInvestigatorFacultyMemberId, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Description = description;
        FundingBodyId = fundingBodyId;
        FundingAmount = fundingAmount;
        FundingPeriod = fundingPeriod;
        Status = GrantStatus.Proposed;
        CreatedAt = now;
        _investigators.Add(new GrantInvestigator(principalInvestigatorFacultyMemberId, GrantInvestigatorRole.PrincipalInvestigator, now));
    }

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Guid FundingBodyId { get; private set; }

    public Money FundingAmount { get; private set; }

    public DateRange FundingPeriod { get; private set; } = null!;

    public GrantStatus Status { get; private set; }

    /// <summary>design-decisions.md's PI-vacancy guard flag - see class remarks.</summary>
    public bool RequiresPiReassignment { get; private set; }

    /// <summary>requirement-spec.md §2 Public Research Showcase: excluded from <c>/api/v1/research/public/grants</c> when <see langword="false"/>, regardless of Status (e.g. a confidential industry grant).</summary>
    public bool IsPubliclyVisible { get; private set; }

    public DateOnly? AwardDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? ClosedAt { get; private set; }

    public DateTimeOffset? ReportedAt { get; private set; }

    public IReadOnlyCollection<GrantInvestigator> Investigators => _investigators.AsReadOnly();

    public Guid? PrincipalInvestigatorFacultyMemberId => _investigators.FirstOrDefault(i => i.Role == GrantInvestigatorRole.PrincipalInvestigator)?.FacultyMemberId;

    public static Grant Propose(string title, string description, Guid fundingBodyId, Money fundingAmount, DateRange fundingPeriod, Guid principalInvestigatorFacultyMemberId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A Grant's title is required.", nameof(title));
        }

        ArgumentNullException.ThrowIfNull(fundingPeriod);

        var grant = new Grant(GrantId.New(), title.Trim(), description?.Trim() ?? string.Empty, fundingBodyId, fundingAmount, fundingPeriod, principalInvestigatorFacultyMemberId, now);
        grant.Raise(new GrantProposed(grant.Id.Value, fundingBodyId, now));
        return grant;
    }

    public void SetPubliclyVisible(bool isPubliclyVisible) => IsPubliclyVisible = isPubliclyVisible;

    /// <summary>requirement-spec.md §2: "the funding body's award decision is recorded (award date, confirmed funding amount/period) - a Research-Office/Admin action, not automatic."</summary>
    public void Fund(DateOnly awardDate, Money confirmedFundingAmount, DateRange confirmedFundingPeriod, DateTimeOffset now)
    {
        EnsureNotPiVacant();

        if (Status != GrantStatus.Proposed)
        {
            throw new InvalidOperationException($"Cannot fund a Grant in status {Status} - only a Proposed Grant may be funded.");
        }

        Status = GrantStatus.Funded;
        AwardDate = awardDate;
        FundingAmount = confirmedFundingAmount;
        FundingPeriod = confirmedFundingPeriod;
        Raise(new GrantFunded(Id.Value, now));
    }

    /// <summary>requirement-spec.md §2: "either the funding period's start date is reached or a PI/Admin explicitly activates early".</summary>
    public void Activate(DateTimeOffset now)
    {
        EnsureNotPiVacant();

        if (Status != GrantStatus.Funded)
        {
            throw new InvalidOperationException($"Cannot activate a Grant in status {Status} - only a Funded Grant may be activated.");
        }

        Status = GrantStatus.Active;
        Raise(new GrantActivated(Id.Value, now));
    }

    public void Close(DateTimeOffset now)
    {
        if (Status is not (GrantStatus.Funded or GrantStatus.Active))
        {
            throw new InvalidOperationException($"Cannot close a Grant in status {Status} - only a Funded or Active Grant may be closed.");
        }

        // design-decisions.md's PI-vacancy decision: Close is one of the two operations
        // deliberately PERMITTED while PI-vacant (the other is Withdraw) - it resolves the
        // vacancy rather than requiring it be resolved first.
        Status = GrantStatus.Closed;
        ClosedAt = now;
        RequiresPiReassignment = false;
        Raise(new GrantClosed(Id.Value, now));
    }

    /// <summary>requirement-spec.md §2: "the PI's final report to the funding body ... a distinct, terminal step from Closed". Only a PI may submit this (enforced by the caller against <see cref="PrincipalInvestigatorFacultyMemberId"/>).</summary>
    public void Report(DateTimeOffset now)
    {
        EnsureNotPiVacant();

        if (Status != GrantStatus.Closed)
        {
            throw new InvalidOperationException($"Cannot report a Grant in status {Status} - only a Closed Grant may be reported.");
        }

        Status = GrantStatus.Reported;
        ReportedAt = now;
        Raise(new GrantReported(Id.Value, now));
    }

    public void Reject(DateTimeOffset now)
    {
        if (Status != GrantStatus.Proposed)
        {
            throw new InvalidOperationException($"Cannot reject a Grant in status {Status} - only a Proposed Grant may be rejected.");
        }

        Status = GrantStatus.Rejected;
        Raise(new GrantRejected(Id.Value, now));
    }

    public void Withdraw(DateTimeOffset now)
    {
        if (Status is not (GrantStatus.Proposed or GrantStatus.Funded or GrantStatus.Active))
        {
            throw new InvalidOperationException($"Cannot withdraw a Grant in status {Status}.");
        }

        // design-decisions.md's PI-vacancy decision: Withdraw is deliberately PERMITTED while
        // PI-vacant - see Close's own remarks above.
        Status = GrantStatus.Withdrawn;
        RequiresPiReassignment = false;
        Raise(new GrantWithdrawn(Id.Value, now));
    }

    /// <summary>
    /// requirement-spec.md §2: "A Co-Investigator may be added/removed at any point before
    /// Reported". Adding with <see cref="GrantInvestigatorRole.PrincipalInvestigator"/> is how a PI
    /// vacancy is resolved - promoting an existing Co-Investigator or naming a new PI - and clears
    /// <see cref="RequiresPiReassignment"/>. Deliberately NOT guarded by
    /// <see cref="EnsureNotPiVacant"/> - this is one of the two operations design-decisions.md's
    /// PI-vacancy decision explicitly permits while vacant.
    /// </summary>
    public void AddInvestigator(Guid facultyMemberId, GrantInvestigatorRole role, DateTimeOffset now)
    {
        EnsureInvestigatorListMutable();

        if (role == GrantInvestigatorRole.PrincipalInvestigator)
        {
            var previousPi = PrincipalInvestigatorFacultyMemberId;

            // Reassignment: drop any existing PI entry and any duplicate Co-Investigator entry for
            // the same person (promotion case) before adding the fresh PI entry.
            _investigators.RemoveAll(i => i.Role == GrantInvestigatorRole.PrincipalInvestigator || i.FacultyMemberId == facultyMemberId);
            _investigators.Add(new GrantInvestigator(facultyMemberId, GrantInvestigatorRole.PrincipalInvestigator, now));
            RequiresPiReassignment = false;
            Raise(new GrantPrincipalInvestigatorChanged(Id.Value, previousPi, facultyMemberId, now));
            return;
        }

        if (_investigators.Any(i => i.FacultyMemberId == facultyMemberId))
        {
            throw new InvalidOperationException($"FacultyMember '{facultyMemberId}' is already an investigator on this Grant.");
        }

        _investigators.Add(new GrantInvestigator(facultyMemberId, GrantInvestigatorRole.CoInvestigator, now));
    }

    /// <summary>
    /// Removing the Principal Investigator is only permitted while <c>Proposed</c> (before the
    /// exactly-one-active-PI invariant applies, §4) - once <c>Funded</c>/<c>Active</c>, a PI
    /// changeover must go through <see cref="AddInvestigator"/>'s reassignment path instead, which
    /// atomically replaces the PI entry rather than leaving the Grant briefly PI-less outside the
    /// explicitly-flagged vacancy window.
    /// </summary>
    public void RemoveInvestigator(Guid facultyMemberId)
    {
        EnsureInvestigatorListMutable();

        var investigator = _investigators.FirstOrDefault(i => i.FacultyMemberId == facultyMemberId);
        if (investigator is null)
        {
            throw new InvalidOperationException($"FacultyMember '{facultyMemberId}' is not an investigator on this Grant.");
        }

        if (investigator.Role == GrantInvestigatorRole.PrincipalInvestigator && Status is not GrantStatus.Proposed && !RequiresPiReassignment)
        {
            throw new InvalidOperationException("The Principal Investigator cannot be removed directly while the Grant is Funded/Active - reassign via AddInvestigator instead.");
        }

        _investigators.Remove(investigator);
    }

    /// <summary>
    /// design-decisions.md's PI-vacancy decision: called by the Application layer when consuming
    /// Faculty's <c>FacultyMemberStatusChanged</c> for a FacultyMember holding the active PI role on
    /// this <c>Funded</c>/<c>Active</c> Grant. Idempotent - a Grant already flagged stays flagged.
    /// </summary>
    public void FlagPiReassignmentRequired(Guid vacatedFacultyMemberId, DateTimeOffset now)
    {
        if (Status is not (GrantStatus.Funded or GrantStatus.Active))
        {
            return;
        }

        if (PrincipalInvestigatorFacultyMemberId != vacatedFacultyMemberId)
        {
            return;
        }

        if (RequiresPiReassignment)
        {
            return;
        }

        RequiresPiReassignment = true;
        Raise(new GrantPiReassignmentRequired(Id.Value, vacatedFacultyMemberId, now));
    }

    private void EnsureNotPiVacant()
    {
        if (RequiresPiReassignment)
        {
            throw new InvalidOperationException("This Grant is flagged as PI-vacant - reassign a Principal Investigator, or close/withdraw the Grant, before any other lifecycle transition.");
        }
    }

    private void EnsureInvestigatorListMutable()
    {
        if (Status is GrantStatus.Reported or GrantStatus.Rejected or GrantStatus.Withdrawn)
        {
            throw new InvalidOperationException($"Cannot change investigators on a Grant in status {Status}.");
        }
    }
}
