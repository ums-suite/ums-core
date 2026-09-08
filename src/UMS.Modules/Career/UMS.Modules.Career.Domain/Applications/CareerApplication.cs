using UMS.Modules.Career.Domain.Common;
using UMS.Modules.Career.Domain.Events;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.Domain.Applications;

/// <summary>
/// CAR-6/CAR-7/CAR-8/CAR-9/CAR-14/CAR-15: the single aggregate root modeling one Student's
/// application, whether against an `Internship` or a `CampusRecruitmentDrive`'s interview process -
/// one shape, two possible targets (requirement-spec.md §2.4, §3, §4).
///
/// <para>
/// <b>Invariant: <see cref="InternshipId"/> XOR <see cref="DriveId"/>, never both.</b> Enforced here
/// (constructor guard) AND by a DB-level CHECK constraint (defense-in-depth, mirroring every other
/// "belt and suspenders" invariant this module applies - see `CareerApplicationConfiguration`).
/// </para>
///
/// <para>
/// <b>No cross-CareerApplication coupling</b> (requirement-spec.md §4): every instance method here
/// only ever mutates THIS aggregate's own state - there is no method anywhere on this class that
/// reaches into another `CareerApplication`.
/// </para>
/// </summary>
public sealed class CareerApplication : AggregateRoot<CareerApplicationId>
{
    private static readonly Dictionary<CareerApplicationStatus, CareerApplicationStatus[]> AllowedStaffTransitions = new()
    {
        [CareerApplicationStatus.Submitted] = [CareerApplicationStatus.UnderReview, CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Rejected],
        [CareerApplicationStatus.UnderReview] = [CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Rejected],
        [CareerApplicationStatus.Shortlisted] = [CareerApplicationStatus.Interviewed, CareerApplicationStatus.Rejected],
        [CareerApplicationStatus.InterviewScheduled] = [CareerApplicationStatus.Interviewed, CareerApplicationStatus.Rejected],
        [CareerApplicationStatus.Interviewed] = [CareerApplicationStatus.Offered, CareerApplicationStatus.Rejected],
    };

    private CareerApplication()
    {
    }

    private CareerApplication(CareerApplicationId id, Guid studentId, InternshipId? internshipId, Domain.Drives.CampusRecruitmentDriveId? driveId, decimal? declaredCgpa, int? declaredYearOfStudy, ResumeSnapshot resume, DateTimeOffset now)
    {
        if (internshipId is null == driveId is null)
        {
            throw new ArgumentException("A CareerApplication must target exactly one of an Internship or a CampusRecruitmentDrive, never both or neither.");
        }

        Id = id;
        StudentId = studentId;
        InternshipId = internshipId;
        DriveId = driveId;
        DeclaredCgpa = declaredCgpa;
        DeclaredYearOfStudy = declaredYearOfStudy;
        ResumeProfileIdSnapshot = resume.ResumeProfileId;
        ResumeArtifactIdSnapshot = resume.ArtifactId;
        ResumeFileNameSnapshot = resume.FileName;
        ResumeSnapshotAt = resume.SnapshotAt;
        Status = CareerApplicationStatus.Submitted;
        SubmittedAt = now;
    }

    public Guid StudentId { get; private set; }

    public InternshipId? InternshipId { get; private set; }

    public Domain.Drives.CampusRecruitmentDriveId? DriveId { get; private set; }

    public CareerApplicationStatus Status { get; private set; }

    /// <summary>design-decisions.md "Self-Declared CGPA/Year-of-Study Eligibility": Student-entered, never externally verified.</summary>
    public decimal? DeclaredCgpa { get; private set; }

    public int? DeclaredYearOfStudy { get; private set; }

    public Guid ResumeProfileIdSnapshot { get; private set; }

    public Guid ResumeArtifactIdSnapshot { get; private set; }

    public string ResumeFileNameSnapshot { get; private set; } = string.Empty;

    public DateTimeOffset ResumeSnapshotAt { get; private set; }

    public ResumeSnapshot Resume => new(ResumeProfileIdSnapshot, ResumeArtifactIdSnapshot, ResumeFileNameSnapshot, ResumeSnapshotAt);

    public Guid? InterviewSlotId { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public string? DecisionReason { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Factory for the Internship path (CAR-6) - kept for unit-testability of the domain invariant; the real write path is a guarded INSERT (edge-cases.md), constructed identically to this shape by the repository.</summary>
    public static CareerApplication SubmitForInternship(Guid studentId, InternshipId internshipId, decimal? declaredCgpa, int? declaredYearOfStudy, ResumeSnapshot resume, DateTimeOffset now)
    {
        var application = new CareerApplication(CareerApplicationId.New(), studentId, internshipId, null, declaredCgpa, declaredYearOfStudy, resume, now);
        application.Raise(new CareerApplicationSubmitted(application.Id.Value, studentId, internshipId.Value, null, now));
        return application;
    }

    /// <summary>Factory for the Drive path (CAR-14) - a Student "registering" for a Drive IS submitting a `CareerApplication` targeting `drive_id` (requirement-spec.md §3's aggregate table names no separate registration entity).</summary>
    public static CareerApplication SubmitForDrive(Guid studentId, Domain.Drives.CampusRecruitmentDriveId driveId, decimal? declaredCgpa, int? declaredYearOfStudy, ResumeSnapshot resume, DateTimeOffset now)
    {
        var application = new CareerApplication(CareerApplicationId.New(), studentId, null, driveId, declaredCgpa, declaredYearOfStudy, resume, now);
        application.Raise(new CareerApplicationSubmitted(application.Id.Value, studentId, null, driveId.Value, now));
        return application;
    }

    /// <summary>CAR-7: staff-driven status review (requirement-spec.md §2.4). `InterviewScheduled` is reached only via <see cref="MarkSlotBooked"/> (CAR-12's atomic write), never through this generic transition.</summary>
    public void TransitionTo(CareerApplicationStatus newStatus, string? reason, DateTimeOffset now)
    {
        if (!AllowedStaffTransitions.TryGetValue(Status, out var allowed) || !allowed.Contains(newStatus))
        {
            throw new InvalidOperationException($"Cannot transition a CareerApplication from {Status} to {newStatus}.");
        }

        var previous = Status;
        Status = newStatus;
        if (newStatus is CareerApplicationStatus.Offered or CareerApplicationStatus.Rejected)
        {
            DecisionReason = reason;
            DecidedAt = now;
        }

        Raise(new CareerApplicationStatusChanged(Id.Value, StudentId, previous.ToString(), newStatus.ToString(), now));
    }

    /// <summary>CAR-12: the atomic conditional-write booking succeeded - this method just records the resulting state transition on the in-memory/tracked aggregate; the concurrency-safe guard itself lives in `IInterviewSlotRepository`'s own atomic SQL (design-decisions.md).</summary>
    public void MarkSlotBooked(Guid slotId, DateTimeOffset now)
    {
        if (Status != CareerApplicationStatus.Shortlisted || InterviewSlotId is not null)
        {
            throw new InvalidOperationException($"Cannot book an InterviewSlot for a CareerApplication in status {Status} (or one that already has a booked slot).");
        }

        InterviewSlotId = slotId;
        var previous = Status;
        Status = CareerApplicationStatus.InterviewScheduled;
        Raise(new CareerApplicationStatusChanged(Id.Value, StudentId, previous.ToString(), Status.ToString(), now));
    }

    /// <summary>CAR-13: a Student releases a booked slot - reverts to `Shortlisted`, freeing them to book a different slot.</summary>
    public void ReleaseSlot(DateTimeOffset now)
    {
        if (Status != CareerApplicationStatus.InterviewScheduled || InterviewSlotId is null)
        {
            throw new InvalidOperationException($"Cannot release an InterviewSlot for a CareerApplication in status {Status} with no booked slot.");
        }

        InterviewSlotId = null;
        var previous = Status;
        Status = CareerApplicationStatus.Shortlisted;
        Raise(new CareerApplicationStatusChanged(Id.Value, StudentId, previous.ToString(), Status.ToString(), now));
    }

    /// <summary>CAR-8: Student-initiated withdrawal - available before any terminal status, no cross-application side effects (requirement-spec.md §4).</summary>
    public void Withdraw(DateTimeOffset now)
    {
        EnsureNonTerminal();
        Status = CareerApplicationStatus.Withdrawn;
        Raise(new CareerApplicationWithdrawn(Id.Value, StudentId, now));
    }

    /// <summary>
    /// CAR-9/CAR-15: the ONLY method that ever writes `Cancelled` - deliberately never shared with
    /// <see cref="TransitionTo"/>'s `Rejected` path, so a future refactor merging the two status-writing
    /// code paths cannot accidentally blur the distinction requirement-spec.md §2.4/§2.6 draws between
    /// them (design-decisions.md "Internship/Drive Withdrawal Cascade", residual note).
    /// </summary>
    public void CancelDueToPostingWithdrawal(DateTimeOffset now)
    {
        EnsureNonTerminal();
        Status = CareerApplicationStatus.Cancelled;
        Raise(new CareerApplicationCancelled(Id.Value, StudentId, now));
    }

    public bool IsTerminal() => Status is CareerApplicationStatus.Offered or CareerApplicationStatus.Rejected or CareerApplicationStatus.Withdrawn or CareerApplicationStatus.Cancelled;

    private void EnsureNonTerminal()
    {
        if (IsTerminal())
        {
            throw new InvalidOperationException($"Cannot mutate a CareerApplication already in terminal status {Status}.");
        }
    }
}
