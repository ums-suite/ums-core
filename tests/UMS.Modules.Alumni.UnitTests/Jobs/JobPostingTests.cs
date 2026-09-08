using UMS.Modules.Alumni.Domain.Jobs;

namespace UMS.Modules.Alumni.UnitTests.Jobs;

/// <summary>requirement-spec.md §2.3/§4/§9: moderation split, expiry-blocks-application invariant.</summary>
public sealed class JobPostingTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Post_by_an_Alumnus_auto_publishes()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);

        Assert.Equal(JobPostingStatus.Published, posting.Status);
        Assert.Single(posting.DomainEvents);
    }

    [Fact]
    public void Post_by_a_non_alumnus_employer_requires_moderation()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: false, null, "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);

        Assert.Equal(JobPostingStatus.PendingModeration, posting.Status);
        Assert.Empty(posting.DomainEvents);
    }

    [Fact]
    public void Approve_transitions_PendingModeration_to_Published()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: false, null, "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);

        posting.Approve(Now.AddHours(1));

        Assert.Equal(JobPostingStatus.Published, posting.Status);
    }

    [Fact]
    public void Approve_a_Published_posting_throws()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);

        Assert.Throws<InvalidOperationException>(() => posting.Approve(Now));
    }

    [Fact]
    public void ExpireIfDue_transitions_Published_past_expiry_and_raises_JobPostingExpired()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(1), Now);

        var expired = posting.ExpireIfDue(Now.AddDays(2));

        Assert.True(expired);
        Assert.Equal(JobPostingStatus.Expired, posting.Status);
        Assert.Contains(posting.DomainEvents, e => e.GetType().Name == "JobPostingExpired");
    }

    [Fact]
    public void ExpireIfDue_is_a_no_op_before_expires_at()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);

        var expired = posting.ExpireIfDue(Now.AddDays(1));

        Assert.False(expired);
        Assert.Equal(JobPostingStatus.Published, posting.Status);
    }

    /// <summary>requirement-spec.md §4: "A JobPosting past its expires_at cannot be applied to."</summary>
    [Fact]
    public void AcceptsApplications_is_false_once_expired()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(1), Now);
        posting.ExpireIfDue(Now.AddDays(2));

        Assert.False(posting.AcceptsApplications(Now.AddDays(2)));
    }

    [Fact]
    public void AcceptsApplications_is_false_for_a_Removed_posting()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);
        posting.Remove("spam");

        Assert.False(posting.AcceptsApplications(Now));
    }

    [Fact]
    public void Edit_after_Removed_throws()
    {
        var posting = JobPosting.Post(Guid.NewGuid(), posterIsAlumnus: true, Guid.NewGuid(), "SWE", "Acme", "desc", "Dhaka", "email", Now.AddDays(30), Now);
        posting.Remove("spam");

        Assert.Throws<InvalidOperationException>(() => posting.Edit("New title", "Acme", "desc", "Dhaka", "email", Now.AddDays(60), Now));
    }
}
