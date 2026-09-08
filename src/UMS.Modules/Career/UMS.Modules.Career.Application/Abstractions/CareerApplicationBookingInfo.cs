namespace UMS.Modules.Career.Application.Abstractions;

/// <summary>
/// A deliberately untracked (`AsNoTracking`) projection used ONLY for pre-flight validation/event
/// payloads around CAR-12/CAR-13's atomic raw-SQL writes - ums-core-gotchas "stale-tracked-entity-
/// after-raw-SQL": a normally-tracked `CareerApplication` read before an
/// `ExecuteSqlInterpolatedAsync` write against the same row would keep showing the PRE-write values
/// in the EF change tracker's first-level cache for the rest of the `DbContext`'s lifetime. Reading
/// only these plain scalar fields sidesteps the problem entirely - there is no tracked entity
/// instance to ever go stale.
/// </summary>
public sealed record CareerApplicationBookingInfo(Guid StudentId, Guid? DriveId, string Status, Guid? InterviewSlotId);
