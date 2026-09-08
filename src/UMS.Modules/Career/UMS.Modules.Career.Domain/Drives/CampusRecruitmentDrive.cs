using UMS.Modules.Career.Domain.Common;
using UMS.Modules.Career.Domain.Events;

namespace UMS.Modules.Career.Domain.Drives;

/// <summary>
/// CAR-10/CAR-11/CAR-15: the `CampusRecruitmentDrive` aggregate root (requirement-spec.md §2.3, §3,
/// §4). Lifecycle `Draft -> Scheduled -> RegistrationOpen -> RegistrationClosed -> Completed`,
/// `Cancelled` reachable from any non-terminal state. Venue resolves against Organization's own
/// `Room` (requirement-spec.md §7) - <see cref="VenueRoomId"/> is an opaque reference, never a
/// competing Career-owned venue concept. Owns child `InterviewSlot`s logically, but they are
/// persisted/queried through their own dedicated repository (see `InterviewSlot`'s own remarks).
/// </summary>
public sealed class CampusRecruitmentDrive : AggregateRoot<CampusRecruitmentDriveId>
{
    private CampusRecruitmentDrive()
    {
    }

    private CampusRecruitmentDrive(CampusRecruitmentDriveId id, Guid employerProfileId, string title, Guid venueRoomId, DateTimeOffset scheduledDate, DateTimeOffset registrationOpensAt, DateTimeOffset registrationClosesAt, DateTimeOffset now)
    {
        Id = id;
        EmployerProfileId = employerProfileId;
        Title = title;
        VenueRoomId = venueRoomId;
        ScheduledDate = scheduledDate;
        RegistrationOpensAt = registrationOpensAt;
        RegistrationClosesAt = registrationClosesAt;
        Status = DriveStatus.Draft;
        CreatedAt = now;
    }

    public Guid EmployerProfileId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public Guid VenueRoomId { get; private set; }

    public DateTimeOffset ScheduledDate { get; private set; }

    public DateTimeOffset RegistrationOpensAt { get; private set; }

    public DateTimeOffset RegistrationClosesAt { get; private set; }

    public DriveStatus Status { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public static CampusRecruitmentDrive Create(Guid employerProfileId, string title, Guid venueRoomId, DateTimeOffset scheduledDate, DateTimeOffset registrationOpensAt, DateTimeOffset registrationClosesAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A CampusRecruitmentDrive's title is required.", nameof(title));
        }

        if (registrationClosesAt <= registrationOpensAt)
        {
            throw new ArgumentException("A CampusRecruitmentDrive's registration window must close after it opens.", nameof(registrationClosesAt));
        }

        return new CampusRecruitmentDrive(CampusRecruitmentDriveId.New(), employerProfileId, title.Trim(), venueRoomId, scheduledDate, registrationOpensAt, registrationClosesAt, now);
    }

    public void Edit(string title, Guid venueRoomId, DateTimeOffset scheduledDate, DateTimeOffset registrationOpensAt, DateTimeOffset registrationClosesAt)
    {
        if (Status != DriveStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot edit a CampusRecruitmentDrive in status {Status} - only a Draft drive may be edited.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A CampusRecruitmentDrive's title is required.", nameof(title));
        }

        if (registrationClosesAt <= registrationOpensAt)
        {
            throw new ArgumentException("A CampusRecruitmentDrive's registration window must close after it opens.", nameof(registrationClosesAt));
        }

        Title = title.Trim();
        VenueRoomId = venueRoomId;
        ScheduledDate = scheduledDate;
        RegistrationOpensAt = registrationOpensAt;
        RegistrationClosesAt = registrationClosesAt;
    }

    public void Schedule(DateTimeOffset now)
    {
        if (Status != DriveStatus.Draft)
        {
            throw new InvalidOperationException($"Cannot schedule a CampusRecruitmentDrive in status {Status} - only a Draft drive may be scheduled.");
        }

        Status = DriveStatus.Scheduled;
        Raise(new CampusRecruitmentDriveScheduled(Id.Value, now));
    }

    public void OpenRegistration()
    {
        if (Status != DriveStatus.Scheduled)
        {
            throw new InvalidOperationException($"Cannot open registration for a CampusRecruitmentDrive in status {Status} - only a Scheduled drive may open registration.");
        }

        Status = DriveStatus.RegistrationOpen;
    }

    public void CloseRegistration()
    {
        if (Status != DriveStatus.RegistrationOpen)
        {
            throw new InvalidOperationException($"Cannot close registration for a CampusRecruitmentDrive in status {Status} - only a RegistrationOpen drive may close registration.");
        }

        Status = DriveStatus.RegistrationClosed;
    }

    public void Complete()
    {
        if (Status != DriveStatus.RegistrationClosed)
        {
            throw new InvalidOperationException($"Cannot complete a CampusRecruitmentDrive in status {Status} - only a RegistrationClosed drive may be completed.");
        }

        Status = DriveStatus.Completed;
    }

    /// <summary>requirement-spec.md §2.3/§2.6: cancellable from any non-terminal state - "the employer visit itself falls through."</summary>
    public void Cancel(string reason, DateTimeOffset now)
    {
        if (Status is DriveStatus.Completed or DriveStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel a CampusRecruitmentDrive in status {Status} - it is already terminal.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A cancellation reason is required.", nameof(reason));
        }

        Status = DriveStatus.Cancelled;
        CancellationReason = reason.Trim();
        Raise(new CampusRecruitmentDriveCancelled(Id.Value, CancellationReason, now));
    }

    /// <summary>requirement-spec.md §2.3: a Student may register interest only while `RegistrationOpen` - the REAL enforcement is the application layer's own guarded-insert against `CareerApplication` (mirrors Internship's own `AcceptsApplications`/guarded-insert split).</summary>
    public bool AcceptsRegistrations() => Status == DriveStatus.RegistrationOpen;
}
