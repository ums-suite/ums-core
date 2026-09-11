using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.UnitTests.Internships;

/// <summary>requirement-spec.md §2.2/§4: `Draft -> Published -> ApplicationsOpen -> ApplicationsClosed`, `Withdrawn` reachable from any non-terminal state; the deadline sweep is idempotent-by-construction.</summary>
public sealed class InternshipTests
{
    private static Internship CreateInternship(DateTimeOffset? deadline = null)
    {
        var now = DateTimeOffset.UtcNow;
        return Internship.Create(Guid.NewGuid(), "Software Engineering Intern", "Great role", "Remote", "Paid", deadline ?? now.AddDays(30), EligibilityCriteria.None, now);
    }

    [Fact]
    public void Create_throws_when_the_deadline_is_not_in_the_future()
    {
        var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => Internship.Create(Guid.NewGuid(), "Intern", "desc", "loc", null, now, EligibilityCriteria.None, now));
    }

    [Fact]
    public void Full_happy_path_lifecycle_progresses_through_every_stage()
    {
        var internship = CreateInternship();

        internship.Publish(DateTimeOffset.UtcNow);
        Assert.Equal(InternshipStatus.Published, internship.Status);
        Assert.NotNull(internship.PublishedAt);
        Assert.True(internship.IsBrowsable());

        internship.OpenApplications();
        Assert.Equal(InternshipStatus.ApplicationsOpen, internship.Status);
        Assert.True(internship.AcceptsApplications(DateTimeOffset.UtcNow));

        internship.CloseApplications(DateTimeOffset.UtcNow);
        Assert.Equal(InternshipStatus.ApplicationsClosed, internship.Status);
        Assert.False(internship.AcceptsApplications(DateTimeOffset.UtcNow));
        Assert.False(internship.IsBrowsable());
    }

    [Fact]
    public void Edit_is_permitted_only_while_Draft()
    {
        var internship = CreateInternship();
        internship.Publish(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => internship.Edit("New title", "desc", "loc", null, DateTimeOffset.UtcNow.AddDays(5), EligibilityCriteria.None, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CloseApplicationsIfDue_is_a_noop_before_the_deadline()
    {
        var internship = CreateInternship(DateTimeOffset.UtcNow.AddDays(30));
        internship.Publish(DateTimeOffset.UtcNow);
        internship.OpenApplications();

        var closed = internship.CloseApplicationsIfDue(DateTimeOffset.UtcNow);

        Assert.False(closed);
        Assert.Equal(InternshipStatus.ApplicationsOpen, internship.Status);
    }

    [Fact]
    public void CloseApplicationsIfDue_transitions_exactly_once_past_the_deadline()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(1);
        var internship = CreateInternship(deadline);
        internship.Publish(DateTimeOffset.UtcNow);
        internship.OpenApplications();

        var evaluatedAt = deadline.AddSeconds(1);
        Assert.True(internship.CloseApplicationsIfDue(evaluatedAt));
        Assert.Equal(InternshipStatus.ApplicationsClosed, internship.Status);

        // Idempotent-by-construction: a second, redundant sweep tick against an already-closed posting is harmless.
        Assert.False(internship.CloseApplicationsIfDue(evaluatedAt));
    }

    [Theory]
    [InlineData(InternshipStatus.Draft)]
    [InlineData(InternshipStatus.Published)]
    [InlineData(InternshipStatus.ApplicationsOpen)]
    public void Withdraw_succeeds_from_every_non_terminal_status(InternshipStatus from)
    {
        var internship = CreateInternship();
        AdvanceTo(internship, from);

        internship.Withdraw("Position filled elsewhere");

        Assert.Equal(InternshipStatus.Withdrawn, internship.Status);
        Assert.Equal("Position filled elsewhere", internship.WithdrawalReason);
    }

    [Fact]
    public void Withdraw_throws_once_ApplicationsClosed()
    {
        var internship = CreateInternship();
        AdvanceTo(internship, InternshipStatus.ApplicationsOpen);
        internship.CloseApplications(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => internship.Withdraw("too late"));
    }

    private static void AdvanceTo(Internship internship, InternshipStatus target)
    {
        if (target == InternshipStatus.Draft)
        {
            return;
        }

        internship.Publish(DateTimeOffset.UtcNow);
        if (target == InternshipStatus.Published)
        {
            return;
        }

        internship.OpenApplications();
    }
}
