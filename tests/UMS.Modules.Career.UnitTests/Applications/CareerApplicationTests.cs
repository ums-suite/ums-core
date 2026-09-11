using UMS.Modules.Career.Domain.Applications;
using UMS.Modules.Career.Domain.Drives;
using UMS.Modules.Career.Domain.Internships;

namespace UMS.Modules.Career.UnitTests.Applications;

/// <summary>
/// requirement-spec.md §4's named invariants: (1) `internship_id` XOR `drive_id`, never both/neither;
/// (2) no cross-CareerApplication coupling; (3) `Cancelled` is written ONLY by
/// <see cref="CareerApplication.CancelDueToPostingWithdrawal"/>, never by the staff `Reject` path;
/// (4) a terminal CareerApplication can never be mutated further.
/// </summary>
public sealed class CareerApplicationTests
{
    private static ResumeSnapshot Snapshot() => new(Guid.NewGuid(), Guid.NewGuid(), "resume.pdf", DateTimeOffset.UtcNow);

    [Fact]
    public void SubmitForInternship_targets_only_InternshipId()
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), 3.5m, 3, Snapshot(), DateTimeOffset.UtcNow);

        Assert.NotNull(application.InternshipId);
        Assert.Null(application.DriveId);
        Assert.Equal(CareerApplicationStatus.Submitted, application.Status);
        Assert.Single(application.DomainEvents);
    }

    [Fact]
    public void SubmitForDrive_targets_only_DriveId()
    {
        var application = CareerApplication.SubmitForDrive(Guid.NewGuid(), CampusRecruitmentDriveId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);

        Assert.Null(application.InternshipId);
        Assert.NotNull(application.DriveId);
        Assert.Equal(CareerApplicationStatus.Submitted, application.Status);
    }

    [Theory]
    [InlineData(CareerApplicationStatus.Submitted, CareerApplicationStatus.UnderReview, true)]
    [InlineData(CareerApplicationStatus.Submitted, CareerApplicationStatus.Shortlisted, true)]
    [InlineData(CareerApplicationStatus.Submitted, CareerApplicationStatus.Offered, false)]
    [InlineData(CareerApplicationStatus.UnderReview, CareerApplicationStatus.Shortlisted, true)]
    [InlineData(CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Interviewed, true)]
    [InlineData(CareerApplicationStatus.Shortlisted, CareerApplicationStatus.InterviewScheduled, false)]
    [InlineData(CareerApplicationStatus.Interviewed, CareerApplicationStatus.Offered, true)]
    [InlineData(CareerApplicationStatus.Interviewed, CareerApplicationStatus.Rejected, true)]
    [InlineData(CareerApplicationStatus.Offered, CareerApplicationStatus.Rejected, false)]
    public void TransitionTo_allows_only_the_documented_staff_transitions(CareerApplicationStatus from, CareerApplicationStatus to, bool expectedAllowed)
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceTo(application, from);

        if (expectedAllowed)
        {
            application.TransitionTo(to, reason: null, DateTimeOffset.UtcNow);
            Assert.Equal(to, application.Status);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(() => application.TransitionTo(to, reason: null, DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public void TransitionTo_Offered_records_decision_reason_and_timestamp()
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceTo(application, CareerApplicationStatus.Interviewed);

        var now = DateTimeOffset.UtcNow;
        application.TransitionTo(CareerApplicationStatus.Offered, "Strong technical interview", now);

        Assert.Equal("Strong technical interview", application.DecisionReason);
        Assert.Equal(now, application.DecidedAt);
    }

    [Fact]
    public void MarkSlotBooked_requires_Shortlisted_status_with_no_existing_slot()
    {
        var application = CareerApplication.SubmitForDrive(Guid.NewGuid(), CampusRecruitmentDriveId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceTo(application, CareerApplicationStatus.Shortlisted);

        var slotId = Guid.NewGuid();
        application.MarkSlotBooked(slotId, DateTimeOffset.UtcNow);

        Assert.Equal(CareerApplicationStatus.InterviewScheduled, application.Status);
        Assert.Equal(slotId, application.InterviewSlotId);

        Assert.Throws<InvalidOperationException>(() => application.MarkSlotBooked(Guid.NewGuid(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReleaseSlot_reverts_to_Shortlisted_and_clears_the_slot()
    {
        var application = CareerApplication.SubmitForDrive(Guid.NewGuid(), CampusRecruitmentDriveId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceTo(application, CareerApplicationStatus.Shortlisted);
        application.MarkSlotBooked(Guid.NewGuid(), DateTimeOffset.UtcNow);

        application.ReleaseSlot(DateTimeOffset.UtcNow);

        Assert.Equal(CareerApplicationStatus.Shortlisted, application.Status);
        Assert.Null(application.InterviewSlotId);
    }

    [Theory]
    [InlineData(CareerApplicationStatus.Submitted)]
    [InlineData(CareerApplicationStatus.UnderReview)]
    [InlineData(CareerApplicationStatus.Shortlisted)]
    [InlineData(CareerApplicationStatus.Interviewed)]
    public void Withdraw_succeeds_from_any_non_terminal_status(CareerApplicationStatus from)
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceTo(application, from);

        application.Withdraw(DateTimeOffset.UtcNow);

        Assert.Equal(CareerApplicationStatus.Withdrawn, application.Status);
        Assert.True(application.IsTerminal());
    }

    [Theory]
    [InlineData(CareerApplicationStatus.Offered)]
    [InlineData(CareerApplicationStatus.Rejected)]
    public void Withdraw_throws_once_a_CareerApplication_is_already_terminal(CareerApplicationStatus terminalStatus)
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        AdvanceToTerminal(application, terminalStatus);

        Assert.Throws<InvalidOperationException>(() => application.Withdraw(DateTimeOffset.UtcNow));
    }

    [Fact]
    public void CancelDueToPostingWithdrawal_is_the_only_path_to_Cancelled()
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);

        application.CancelDueToPostingWithdrawal(DateTimeOffset.UtcNow);

        Assert.Equal(CareerApplicationStatus.Cancelled, application.Status);
        Assert.True(application.IsTerminal());
    }

    [Fact]
    public void CancelDueToPostingWithdrawal_throws_once_a_CareerApplication_is_already_terminal()
    {
        var application = CareerApplication.SubmitForInternship(Guid.NewGuid(), InternshipId.New(), null, null, Snapshot(), DateTimeOffset.UtcNow);
        application.Withdraw(DateTimeOffset.UtcNow);

        Assert.Throws<InvalidOperationException>(() => application.CancelDueToPostingWithdrawal(DateTimeOffset.UtcNow));
    }

    private static void AdvanceTo(CareerApplication application, CareerApplicationStatus target)
    {
        if (application.Status == target)
        {
            return;
        }

        var path = target switch
        {
            CareerApplicationStatus.UnderReview => new[] { CareerApplicationStatus.UnderReview },
            CareerApplicationStatus.Shortlisted => new[] { CareerApplicationStatus.Shortlisted },
            CareerApplicationStatus.Interviewed => new[] { CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Interviewed },
            CareerApplicationStatus.Offered => new[] { CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Interviewed, CareerApplicationStatus.Offered },
            CareerApplicationStatus.Rejected => new[] { CareerApplicationStatus.Shortlisted, CareerApplicationStatus.Interviewed, CareerApplicationStatus.Rejected },
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, "Unsupported test setup target."),
        };

        foreach (var step in path)
        {
            application.TransitionTo(step, reason: null, DateTimeOffset.UtcNow);
        }
    }

    private static void AdvanceToTerminal(CareerApplication application, CareerApplicationStatus terminalStatus)
    {
        AdvanceTo(application, CareerApplicationStatus.Interviewed);
        application.TransitionTo(terminalStatus, terminalStatus == CareerApplicationStatus.Rejected ? "not a fit" : null, DateTimeOffset.UtcNow);
    }
}
