using UMS.Modules.Academic.Domain.Common;

namespace UMS.Modules.Academic.Application.Abstractions;

/// <summary>
/// Lets an application service enqueue a domain event directly, for the (small) set of writes that
/// bypass EF's change tracker (the state-guarded conditional updates on <c>CourseOffering</c>'s
/// seat counter and <c>ResultPublication</c>'s status - <c>ExecuteUpdateAsync</c> never populates
/// <c>ChangeTracker.Entries&lt;IHasDomainEvents&gt;()</c>, so the DbContext's normal
/// "drain each tracked aggregate's raised events into the outbox on SaveChanges" mechanism has
/// nothing to drain for those calls). Every OTHER write path (Program, Course, Curriculum,
/// AcademicSession, Enrollment status changes, AttendanceSession) still raises events the ordinary
/// way, via the aggregate's own <c>Raise</c> - this recorder is only for the two exceptions.
/// </summary>
public interface IDomainEventRecorder
{
    public void Enqueue(IDomainEvent domainEvent);
}
