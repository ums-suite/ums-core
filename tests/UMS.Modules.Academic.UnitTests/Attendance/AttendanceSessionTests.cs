using UMS.Modules.Academic.Domain.Attendance;

namespace UMS.Modules.Academic.UnitTests.Attendance;

/// <summary>edge-cases.md "Attendance correction-window-close racing a late marking attempt" - a late attempt is rejected outright, never silently accepted or dropped.</summary>
public sealed class AttendanceSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UtcNow;

    [Fact]
    public void MarkOrUpdate_within_the_correction_window_succeeds()
    {
        var session = AttendanceSession.Create(Guid.NewGuid(), DateOnly.FromDateTime(Now.UtcDateTime), Now.AddHours(1), Now);

        var record = session.MarkOrUpdate(Guid.NewGuid(), AttendanceStatus.Present, Guid.NewGuid(), Now);

        Assert.Equal(AttendanceStatus.Present, record.Status);
        Assert.Single(session.Records);
    }

    [Fact]
    public void MarkOrUpdate_after_the_correction_window_closes_throws_and_never_silently_accepts()
    {
        var session = AttendanceSession.Create(Guid.NewGuid(), DateOnly.FromDateTime(Now.UtcDateTime), Now.AddMinutes(-1), Now.AddMinutes(-2));

        Assert.Throws<InvalidOperationException>(() => session.MarkOrUpdate(Guid.NewGuid(), AttendanceStatus.Present, Guid.NewGuid(), Now));
        Assert.Empty(session.Records);
    }

    [Fact]
    public void MarkOrUpdate_called_twice_for_the_same_Enrollment_updates_in_place_rather_than_duplicating()
    {
        var session = AttendanceSession.Create(Guid.NewGuid(), DateOnly.FromDateTime(Now.UtcDateTime), Now.AddHours(1), Now);
        var enrollmentId = Guid.NewGuid();
        session.MarkOrUpdate(enrollmentId, AttendanceStatus.Absent, Guid.NewGuid(), Now);

        session.MarkOrUpdate(enrollmentId, AttendanceStatus.Late, Guid.NewGuid(), Now);

        Assert.Single(session.Records);
        Assert.Equal(AttendanceStatus.Late, session.Records.Single().Status);
    }

    [Fact]
    public void An_edit_attempt_exactly_at_the_correction_window_close_instant_still_succeeds()
    {
        var closeInstant = Now.AddHours(1);
        var session = AttendanceSession.Create(Guid.NewGuid(), DateOnly.FromDateTime(Now.UtcDateTime), closeInstant, Now);

        var record = session.MarkOrUpdate(Guid.NewGuid(), AttendanceStatus.Present, Guid.NewGuid(), closeInstant);

        Assert.Equal(AttendanceStatus.Present, record.Status);
    }
}
