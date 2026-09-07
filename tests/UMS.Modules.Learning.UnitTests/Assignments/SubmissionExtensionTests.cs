using UMS.Modules.Learning.Domain.Assignments;
using UMS.Modules.Learning.Domain.Events;

namespace UMS.Modules.Learning.UnitTests.Assignments;

/// <summary>LRN-3 / design-decisions.md "Per-Student Submission Extension (Accommodation) Design": a first-class, audited, per-<c>(Assignment, Student)</c> entity - never an ad-hoc widening of the shared window.</summary>
public sealed class SubmissionExtensionTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Granting_an_extension_raises_AssignmentExtensionGranted()
    {
        var assignment = PublishedAssignment();
        assignment.ClearDomainEvents();

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(5), Guid.NewGuid(), "Hospitalized.", waivesLatePenalty: false, Now);

        Assert.True(granted.IsSuccess);
        Assert.Single(assignment.DomainEvents.OfType<AssignmentExtensionGranted>());
    }

    [Fact]
    public void An_extension_without_a_reason_is_rejected()
    {
        var assignment = PublishedAssignment();

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(5), Guid.NewGuid(), "   ", waivesLatePenalty: false, Now);

        Assert.True(granted.IsFailure);
        Assert.Equal("submission_extension.reason_required", granted.Error!.Code);
    }

    [Fact]
    public void An_extension_with_a_deadline_already_in_the_past_is_rejected()
    {
        var assignment = PublishedAssignment();

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(-1), Guid.NewGuid(), "Backdated.", waivesLatePenalty: false, Now);

        Assert.True(granted.IsFailure);
        Assert.Equal("submission_extension.deadline_in_the_past", granted.Error!.Code);
    }

    /// <summary>requirement-spec.md §4 scopes an extension to exactly one (Assignment, Student) pair - so a re-grant replaces rather than stacks, keeping "the later of base window and extension" unambiguous.</summary>
    [Fact]
    public void Re_granting_for_the_same_Student_replaces_the_prior_extension_rather_than_stacking()
    {
        var assignment = PublishedAssignment();
        var student = Guid.NewGuid();
        assignment.GrantExtension(student, Guid.NewGuid(), Now.AddDays(2), Guid.NewGuid(), "First grant.", waivesLatePenalty: false, Now);

        assignment.GrantExtension(student, Guid.NewGuid(), Now.AddDays(6), Guid.NewGuid(), "Revised grant.", waivesLatePenalty: true, Now);

        Assert.Single(assignment.Extensions);
        var extension = assignment.ExtensionFor(student)!;
        Assert.Equal(Now.AddDays(6), extension.ExtendedDeadline);
        Assert.True(extension.WaivesLatePenalty);
    }

    [Fact]
    public void Extensions_for_two_Students_coexist_independently()
    {
        var assignment = PublishedAssignment();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        assignment.GrantExtension(first, Guid.NewGuid(), Now.AddDays(2), Guid.NewGuid(), "A.", waivesLatePenalty: false, Now);
        assignment.GrantExtension(second, Guid.NewGuid(), Now.AddDays(4), Guid.NewGuid(), "B.", waivesLatePenalty: false, Now);

        Assert.Equal(2, assignment.Extensions.Count);
        Assert.Equal(Now.AddDays(2), assignment.ExtensionFor(first)!.ExtendedDeadline);
        Assert.Equal(Now.AddDays(4), assignment.ExtensionFor(second)!.ExtendedDeadline);
    }

    [Fact]
    public void An_extension_on_a_Draft_Assignment_is_rejected()
    {
        var assignment = AssignmentTests.Create();

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(5), Guid.NewGuid(), "Too early.", waivesLatePenalty: false, Now);

        Assert.True(granted.IsFailure);
        Assert.Equal("assignment.extension_on_draft_assignment", granted.Error!.Code);
    }

    [Fact]
    public void An_extension_on_a_Cancelled_Assignment_is_rejected()
    {
        var assignment = PublishedAssignment();
        assignment.Cancel("Offering withdrawn.", Now);

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(5), Guid.NewGuid(), "Too late.", waivesLatePenalty: false, Now);

        Assert.True(granted.IsFailure);
        Assert.Equal("assignment.extension_on_cancelled_assignment", granted.Error!.Code);
    }

    /// <summary>An extension on a CLOSED Assignment is deliberately allowed - reopening the window for exactly one Student after a class-wide close is the accommodation case requirement-spec.md §2 names.</summary>
    [Fact]
    public void An_extension_on_a_Closed_Assignment_is_allowed()
    {
        var assignment = PublishedAssignment();
        assignment.Close(Now);

        var granted = assignment.GrantExtension(Guid.NewGuid(), Guid.NewGuid(), Now.AddDays(5), Guid.NewGuid(), "Post-close accommodation.", waivesLatePenalty: true, Now);

        Assert.True(granted.IsSuccess);
    }

    private static Assignment PublishedAssignment()
    {
        var assignment = AssignmentTests.Create();
        assignment.Publish(Now);
        return assignment;
    }
}
