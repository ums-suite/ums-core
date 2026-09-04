using UMS.Modules.Faculty.Domain.CourseAssignments;

namespace UMS.Modules.Faculty.UnitTests.CourseAssignments;

/// <summary>design-decisions.md, "Event Consumer Idempotency and Ordering": the monotonic ordering guard, exercised directly against the aggregate.</summary>
public sealed class CourseAssignmentTests
{
    private static readonly DateTimeOffset T0 = DateTimeOffset.UtcNow;

    [Fact]
    public void CreateFromAssigned_is_Active()
    {
        var courseAssignment = CourseAssignment.CreateFromAssigned(Guid.NewGuid(), Guid.NewGuid(), T0);

        Assert.Equal(CourseAssignmentStatus.Active, courseAssignment.Status);
    }

    [Fact]
    public void ApplyUnassigned_after_assign_transitions_to_Ended()
    {
        var courseAssignment = CourseAssignment.CreateFromAssigned(Guid.NewGuid(), Guid.NewGuid(), T0);

        var applied = courseAssignment.ApplyUnassigned(T0.AddMinutes(1));

        Assert.True(applied);
        Assert.Equal(CourseAssignmentStatus.Ended, courseAssignment.Status);
    }

    [Fact]
    public void A_stale_older_event_is_a_no_op()
    {
        var courseAssignment = CourseAssignment.CreateFromAssigned(Guid.NewGuid(), Guid.NewGuid(), T0);
        courseAssignment.ApplyUnassigned(T0.AddMinutes(5));

        // An out-of-order, older Unassigned arriving after the newer state already applied.
        var applied = courseAssignment.ApplyUnassigned(T0.AddMinutes(1));

        Assert.False(applied);
        Assert.Equal(CourseAssignmentStatus.Ended, courseAssignment.Status);
    }

    [Fact]
    public void A_duplicate_delivery_of_the_same_event_time_is_a_no_op()
    {
        var courseAssignment = CourseAssignment.CreateFromAssigned(Guid.NewGuid(), Guid.NewGuid(), T0);

        var appliedAgain = courseAssignment.ApplyAssigned(T0);

        Assert.False(appliedAgain);
    }

    [Fact]
    public void Reassignment_after_an_end_reactivates_the_same_projection_row()
    {
        var courseAssignment = CourseAssignment.CreateFromAssigned(Guid.NewGuid(), Guid.NewGuid(), T0);
        courseAssignment.ApplyUnassigned(T0.AddMinutes(1));

        var applied = courseAssignment.ApplyAssigned(T0.AddMinutes(2));

        Assert.True(applied);
        Assert.Equal(CourseAssignmentStatus.Active, courseAssignment.Status);
        Assert.Null(courseAssignment.EndedAt);
    }
}
