using UMS.Modules.Alumni.Domain.Common;
using UMS.Modules.Alumni.Domain.Events;

namespace UMS.Modules.Alumni.Domain.Jobs;

/// <summary>
/// ALM-5/ALM-6: the JobPosting aggregate root (requirement-spec.md §2.3, §3, §4).
///
/// <para>
/// design-decisions.md "Job-Posting Moderation-Queue Concurrency Control": every state-changing
/// method here is meant to be called under an optimistic <c>xmin</c> version check
/// (<c>IUnitOfWork.SetExpectedVersion</c>) applied uniformly by the caller - a moderation action and
/// a poster's own edit race the SAME aggregate instance, mirroring Content's own <c>Notice</c> and
/// Research's own <c>Grant</c> exactly (edge-cases.md "Two employers/alumni racing to claim/moderate
/// the same JobPosting").
/// </para>
///
/// <para>
/// requirement-spec.md §2.3/§9: an alumnus-posted job auto-publishes with post-hoc moderation; a
/// non-alumnus employer posting is pre-moderated (<c>PendingModeration</c> until an Admin approves).
/// </para>
/// </summary>
public sealed class JobPosting : AggregateRoot<JobPostingId>
{
    private JobPosting()
    {
    }

    private JobPosting(JobPostingId id, Guid posterUserId, bool posterIsAlumnus, Guid? posterAlumnusId, string title, string company, string description, string location, string contactMethod, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        Id = id;
        PosterUserId = posterUserId;
        PosterIsAlumnus = posterIsAlumnus;
        PosterAlumnusId = posterAlumnusId;
        Title = title;
        Company = company;
        Description = description;
        Location = location;
        ContactMethod = contactMethod;
        ExpiresAt = expiresAt;
        CreatedAt = now;
    }

    public Guid PosterUserId { get; private set; }

    public bool PosterIsAlumnus { get; private set; }

    public Guid? PosterAlumnusId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string Company { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Location { get; private set; } = string.Empty;

    public string ContactMethod { get; private set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; private set; }

    public JobPostingStatus Status { get; private set; }

    public string? ModerationReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>requirement-spec.md §9: alumnus-posted jobs auto-publish; non-alumnus employer postings require Admin approval first.</summary>
    public static JobPosting Post(Guid posterUserId, bool posterIsAlumnus, Guid? posterAlumnusId, string title, string company, string description, string location, string contactMethod, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A JobPosting's title is required.", nameof(title));
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException("A JobPosting's expires_at must be in the future.", nameof(expiresAt));
        }

        var posting = new JobPosting(JobPostingId.New(), posterUserId, posterIsAlumnus, posterAlumnusId, title.Trim(), company?.Trim() ?? string.Empty, description?.Trim() ?? string.Empty, location?.Trim() ?? string.Empty, contactMethod?.Trim() ?? string.Empty, expiresAt, now);

        if (posterIsAlumnus)
        {
            posting.Status = JobPostingStatus.Published;
            posting.PublishedAt = now;
            posting.Raise(new JobPostingPublished(posting.Id.Value, now));
        }
        else
        {
            posting.Status = JobPostingStatus.PendingModeration;
        }

        return posting;
    }

    /// <summary>ALM-5: the poster's own edit - permitted only while the posting is still mutable (not yet a terminal state). Concurrency-checked identically to a moderation action (design-decisions.md).</summary>
    public void Edit(string title, string company, string description, string location, string contactMethod, DateTimeOffset expiresAt, DateTimeOffset now)
    {
        EnsureEditable();

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("A JobPosting's title is required.", nameof(title));
        }

        if (expiresAt <= now)
        {
            throw new ArgumentException("A JobPosting's expires_at must be in the future.", nameof(expiresAt));
        }

        Title = title.Trim();
        Company = company?.Trim() ?? string.Empty;
        Description = description?.Trim() ?? string.Empty;
        Location = location?.Trim() ?? string.Empty;
        ContactMethod = contactMethod?.Trim() ?? string.Empty;
        ExpiresAt = expiresAt;
    }

    public void Approve(DateTimeOffset now)
    {
        if (Status != JobPostingStatus.PendingModeration)
        {
            throw new InvalidOperationException($"Cannot approve a JobPosting in status {Status} - only a PendingModeration posting may be approved.");
        }

        Status = JobPostingStatus.Published;
        PublishedAt = now;
        Raise(new JobPostingPublished(Id.Value, now));
    }

    public void Reject(string reason)
    {
        if (Status != JobPostingStatus.PendingModeration)
        {
            throw new InvalidOperationException($"Cannot reject a JobPosting in status {Status} - only a PendingModeration posting may be rejected.");
        }

        Status = JobPostingStatus.Removed;
        ModerationReason = reason;
    }

    /// <summary>requirement-spec.md §2.3 last bullet / edge-cases.md: Admin can remove any posting at any time, even after publication (retroactive spam/scam removal).</summary>
    public void Remove(string reason)
    {
        if (Status is JobPostingStatus.Removed)
        {
            return;
        }

        Status = JobPostingStatus.Removed;
        ModerationReason = reason;
    }

    public void Withdraw()
    {
        EnsureEditable();
        Status = JobPostingStatus.Removed;
        ModerationReason = "withdrawn_by_poster";
    }

    /// <summary>ALM-6: the scheduled expiry sweep (requirement-spec.md §2.3) - idempotent-by-construction, only a Published posting past its own expires_at transitions.</summary>
    public bool ExpireIfDue(DateTimeOffset now)
    {
        if (Status != JobPostingStatus.Published || ExpiresAt > now)
        {
            return false;
        }

        Status = JobPostingStatus.Expired;
        Raise(new JobPostingExpired(Id.Value, now));
        return true;
    }

    /// <summary>requirement-spec.md §4: "A JobPosting past its expires_at cannot be applied to - enforced at write time." Used by the application layer's own guarded-insert re-check (edge-cases.md "JobPosting expiry sweep racing a concurrent application submission").</summary>
    public bool AcceptsApplications(DateTimeOffset now) => Status == JobPostingStatus.Published && ExpiresAt > now;

    private void EnsureEditable()
    {
        if (Status is JobPostingStatus.Expired or JobPostingStatus.Removed)
        {
            throw new InvalidOperationException($"Cannot edit/withdraw a JobPosting in status {Status}.");
        }
    }
}
