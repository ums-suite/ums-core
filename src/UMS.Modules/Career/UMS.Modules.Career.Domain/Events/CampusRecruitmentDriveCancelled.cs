using UMS.Modules.Career.Domain.Common;

namespace UMS.Modules.Career.Domain.Events;

/// <summary>
/// requirement-spec.md §3/§2.6; edge-cases.md "employer withdraws an Internship" (resolved bullet:
/// same mechanism for Drive cancellation); design-decisions.md "Internship/Drive Withdrawal Cascade".
/// Handled in-process by <c>Applications.DriveCancellationCascadeHandler</c> - identical cascade
/// mechanism to <see cref="InternshipWithdrawn"/>, additionally releasing any booked
/// <c>InterviewSlot</c>s' capacity atomically. Consumers: Notifications (per registrant/applicant), Reporting.
/// </summary>
public sealed record CampusRecruitmentDriveCancelled(Guid DriveId, string Reason, DateTimeOffset OccurredAt) : IDomainEvent;
