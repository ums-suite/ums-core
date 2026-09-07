using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Events;

namespace UMS.Modules.Learning.UnitTests.Assignments;

/// <summary>LRN-1/LRN-2: the Draft -&gt; Published -&gt; Closed lifecycle plus the explicit Cancelled terminal state (requirement-spec.md learning §2).</summary>
public sealed class AssignmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_new_Assignment_starts_in_Draft_and_raises_nothing_until_it_is_published()
    {
        var assignment = Create();

        Assert.Equal(AssignmentStatus.Draft, assignment.Status);
        Assert.Empty(assignment.DomainEvents);
    }

    [Fact]
    public void A_blank_title_is_rejected()
    {
        var result = Assignment.Create(
            Guid.NewGuid(),
            "   ",
            "Do the thing.",
            AllowedSubmissionType.TextOrFile,
            allowResubmission: true,
            maxPoints: 100,
            Window(),
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.title_required", result.Error!.Code);
    }

    [Fact]
    public void A_max_points_below_one_is_rejected()
    {
        var result = Assignment.Create(
            Guid.NewGuid(),
            "Essay",
            string.Empty,
            AllowedSubmissionType.Text,
            allowResubmission: false,
            maxPoints: 0,
            Window(),
            Guid.NewGuid(),
            Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.max_points_out_of_range", result.Error!.Code);
    }

    [Fact]
    public void Publishing_a_Draft_transitions_it_and_raises_AssignmentPublished()
    {
        var assignment = Create();

        var result = assignment.Publish(Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Published, assignment.Status);
        Assert.Equal(Now, assignment.PublishedAt);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentPublished>());
    }

    [Fact]
    public void Publishing_an_already_Published_Assignment_is_rejected()
    {
        var assignment = Create();
        assignment.Publish(Now);

        var result = assignment.Publish(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.invalid_transition", result.Error!.Code);
    }

    [Fact]
    public void Closing_a_Published_Assignment_raises_AssignmentClosed()
    {
        var assignment = Create();
        assignment.Publish(Now);
        assignment.ClearDomainEvents();

        var result = assignment.Close(Now.AddDays(10));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Closed, assignment.Status);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentClosed>());
    }

    [Fact]
    public void Closing_a_Draft_Assignment_is_rejected()
    {
        var assignment = Create();

        var result = assignment.Close(Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.invalid_transition", result.Error!.Code);
    }

    /// <summary>edge-cases.md "A CourseOffering is cancelled mid-semester" - the manual first-pass path, since Academic defines no CourseOfferingCancelled event for Learning to subscribe to.</summary>
    [Fact]
    public void Cancelling_records_the_reason_and_raises_AssignmentCancelled()
    {
        var assignment = Create();
        assignment.Publish(Now);
        assignment.ClearDomainEvents();

        var result = assignment.Cancel("CourseOffering withdrawn for low enrollment.", Now.AddDays(2));

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
        Assert.Equal("CourseOffering withdrawn for low enrollment.", assignment.CancellationReason);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentCancelled>());
    }

    [Fact]
    public void Cancelling_without_a_reason_is_rejected()
    {
        var assignment = Create();
        assignment.Publish(Now);

        var result = assignment.Cancel("  ", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.cancellation_reason_required", result.Error!.Code);
    }

    [Fact]
    public void Cancelling_an_already_Cancelled_Assignment_is_rejected()
    {
        var assignment = Create();
        assignment.Publish(Now);
        assignment.Cancel("First cancellation.", Now);

        var result = assignment.Cancel("Second cancellation.", Now);

        Assert.True(result.IsFailure);
        Assert.Equal("assignment.invalid_transition", result.Error!.Code);
    }

    /// <summary>A Draft Assignment may still be cancelled outright - an Instructor who abandons a task before publishing it should not have to publish it first just to cancel it.</summary>
    [Fact]
    public void A_Draft_Assignment_may_be_cancelled_directly()
    {
        var assignment = Create();

        var result = assignment.Cancel("Abandoned before publication.", Now);

        Assert.True(result.IsSuccess);
        Assert.Equal(AssignmentStatus.Cancelled, assignment.Status);
    }

    internal static SubmissionWindow Window(
        DateTimeOffset? opensAt = null,
        DateTimeOffset? deadline = null,
        TimeSpan? gracePeriod = null,
        LatePenaltyPolicy? policy = null,
        DateTimeOffset? hardCloseAt = null)
    {
        var opens = opensAt ?? Now;
        var due = deadline ?? Now.AddDays(9);
        return SubmissionWindow.Create(
            opens,
            due,
            gracePeriod ?? TimeSpan.FromMinutes(5),
            policy ?? LatePenaltyPolicy.NoDeduction,
            hardCloseAt ?? due.AddDays(3)).Value;
    }

    internal static Assignment Create(
        SubmissionWindow? window = null,
        AllowedSubmissionType allowedSubmissionType = AllowedSubmissionType.TextOrFile,
        bool allowResubmission = true,
        int maxPoints = 100,
        Guid? createdByUserId = null) =>
        Assignment.Create(
            Guid.NewGuid(),
            "Essay on distributed systems",
            "Write 2000 words.",
            allowedSubmissionType,
            allowResubmission,
            maxPoints,
            window ?? Window(),
            createdByUserId ?? Guid.NewGuid(),
            Now).Value;
}
