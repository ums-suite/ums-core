using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Domain.Notices;

/// <summary>
/// CNT-1/2/3/4/5/15: glossary "a publishable, schedulable, archivable announcement with
/// translations." requirement-spec.md §3: <c>Draft -&gt; Scheduled -&gt; Published -&gt; Archived</c>.
///
/// <para>
/// <b>Concurrency mechanism - read this before changing any writer of this aggregate.</b>
/// design-decisions.md "Concurrent-Edit Conflict Resolution": every writer - a human Admin's save,
/// another Admin's concurrent save, AND the scheduled publish/expire job's own transition
/// (<see cref="Publish"/>/<see cref="Archive"/> called from
/// <c>Application.Scheduling.NoticeSchedulingService</c>) - is an equal-standing writer subject to
/// the identical <see cref="AggregateRoot{TId}.Version"/> (`xmin`) check. No writer is
/// special-cased. There is deliberately NO distributed lock/lease anywhere in this module
/// (design-decisions.md "Scheduled-Publish Job Exactly-Once Execution Mechanism") - a second
/// concurrent job tick against an already-transitioned row simply loses the version race harmlessly.
/// </para>
///
/// <para>
/// <b>Bilingual-completeness gate</b> (design-decisions.md "Bilingual-Completeness Gate Enforcement
/// Point"): both <see cref="Schedule"/> and <see cref="Publish"/> independently call
/// <see cref="BilingualCompletenessGate.IsSatisfied"/> every time they run - <see cref="Publish"/>
/// is invoked identically by the manual `/publish` endpoint AND the scheduled job's own
/// `Scheduled -&gt; Published` commit, so the check genuinely re-executes at all three transition
/// moments named by edge-cases.md, never trusting an earlier pass to still hold.
/// </para>
/// </summary>
public sealed class Notice : AggregateRoot<NoticeId>
{
    private readonly List<NoticeTranslation> _translations = [];

    private Notice()
    {
    }

    private Notice(NoticeId id, string title, string body, ContentAudience audience, Guid? organizationNodeId, bool isUrgent, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Body = body;
        Audience = audience;
        OrganizationNodeId = organizationNodeId;
        IsUrgent = isUrgent;
        Status = SchedulableStatus.Draft;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public ContentAudience Audience { get; private set; }

    /// <summary>requirement-spec.md §2.1: further scopes a Student/Faculty-audience Notice to one Organization node (e.g. a Department). Null means "every caller holding the audience, university-wide."</summary>
    public Guid? OrganizationNodeId { get; private set; }

    public bool IsUrgent { get; private set; }

    public SchedulableStatus Status { get; private set; }

    public DateTimeOffset? PublishAt { get; private set; }

    public DateTimeOffset? ExpireAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public IReadOnlyCollection<NoticeTranslation> Translations => _translations.AsReadOnly();

    public static Result<Notice> Create(string title, string body, ContentAudience audience, Guid? organizationNodeId, bool isUrgent, Guid createdByUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Error.Validation("notice.title_required", "A Notice requires a title.");
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Error.Validation("notice.body_required", "A Notice requires body text.");
        }

        if (audience == ContentAudience.None)
        {
            return Error.Validation("notice.audience_required", "A Notice must target at least one audience.");
        }

        return new Notice(NoticeId.New(), title.Trim(), body.Trim(), audience, organizationNodeId, isUrgent, createdByUserId, now);
    }

    /// <summary>
    /// requirement-spec.md §2.1: metadata correction is allowed at any non-terminal status,
    /// including after publication (recorded via `updated_at`/`updated_by` - the caller's own
    /// audit write happens at the application layer, keyed off <see cref="Status"/> being
    /// <see cref="SchedulableStatus.Published"/> at the time of the call).
    /// </summary>
    public Result EditContent(string title, string body, Guid actorUserId, DateTimeOffset now)
    {
        if (Status == SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("notice.archived", "An Archived Notice's content can no longer be edited."));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("notice.title_required", "A Notice requires a title."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure(Error.Validation("notice.body_required", "A Notice requires body text."));
        }

        Title = title.Trim();
        Body = body.Trim();
        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>CNT-2: upserts the one `(languageCode)` translation row - see <see cref="NoticeTranslation"/>'s own remarks on why English itself is never stored here.</summary>
    public Result UpsertTranslation(string languageCode, string title, string body, Guid actorUserId, DateTimeOffset now)
    {
        if (Status == SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("notice.archived", "An Archived Notice's translations can no longer be edited."));
        }

        if (string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation("notice.translation_english_not_allowed", "English is the Notice's own canonical Title/Body - it is not stored as a translation row."));
        }

        var existing = _translations.FirstOrDefault(t => string.Equals(t.LanguageCode, languageCode!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(title, body);
        }
        else
        {
            var created = NoticeTranslation.Create(languageCode!, title, body);
            _translations.Add(created);
        }

        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>requirement-spec.md §4: a scheduling window must be non-empty - `expire_at` (when set) must be strictly after `publish_at`.</summary>
    public Result UpdateSchedule(DateTimeOffset? publishAt, DateTimeOffset? expireAt, DateTimeOffset now)
    {
        if (Status is SchedulableStatus.Published or SchedulableStatus.Archived)
        {
            return Result.Failure(Error.Conflict("notice.not_schedulable", $"Notice '{Id}' cannot have its schedule changed - it is currently '{Status}'."));
        }

        if (publishAt is null && expireAt is not null)
        {
            return Result.Failure(Error.Validation("notice.expire_without_publish", "An expire_at cannot be set without a publish_at."));
        }

        if (publishAt is not null && expireAt is not null && expireAt <= publishAt)
        {
            return Result.Failure(Error.Validation("notice.invalid_window", "expire_at must be strictly after publish_at."));
        }

        PublishAt = publishAt;
        ExpireAt = expireAt;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>CNT-1/CNT-2: `Draft -&gt; Scheduled`. First of the bilingual-completeness gate's three independent call sites.</summary>
    public Result Schedule(DateTimeOffset now)
    {
        if (Status != SchedulableStatus.Draft)
        {
            return Result.Failure(Error.Conflict("notice.not_draft", $"Notice '{Id}' cannot be scheduled - it is currently '{Status}'."));
        }

        if (PublishAt is null)
        {
            return Result.Failure(Error.Validation("notice.publish_at_required", "A publish_at is required to schedule a Notice."));
        }

        if (!BilingualCompletenessGate.IsSatisfied(Audience, Title, Body, _translations.Select(t => t.LanguageCode)))
        {
            return Result.Failure(Error.Validation("notice.bilingual_incomplete", "A Public-audience Notice requires both English and Bengali translations before it can leave Draft."));
        }

        Status = SchedulableStatus.Scheduled;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>
    /// CNT-1/CNT-5: `Draft -&gt; Published` or `Scheduled -&gt; Published`. Called identically by
    /// the manual `/publish` endpoint (bypassing the schedule) and by the scheduled job's own
    /// `Scheduled -&gt; Published` transition - the SAME method, so the bilingual gate below is the
    /// job's defensive re-check, not a separate copy of it (edge-cases.md "A Notice's translation
    /// exists in only one language when publish_at fires": "left in Scheduled state rather than
    /// force-published incomplete" - a failed gate here simply returns a Conflict, and the caller
    /// leaves the row exactly as it was).
    /// </summary>
    public Result Publish(DateTimeOffset now)
    {
        if (Status is not (SchedulableStatus.Draft or SchedulableStatus.Scheduled))
        {
            return Result.Failure(Error.Conflict("notice.not_publishable", $"Notice '{Id}' cannot be published - it is currently '{Status}'."));
        }

        if (!BilingualCompletenessGate.IsSatisfied(Audience, Title, Body, _translations.Select(t => t.LanguageCode)))
        {
            return Result.Failure(Error.Validation("notice.bilingual_incomplete", "A Public-audience Notice requires both English and Bengali translations before it can be Published."));
        }

        Status = SchedulableStatus.Published;
        PublishedAt = now;
        UpdatedAt = now;
        Raise(new NoticePublished(Id.Value, IsUrgent, Audience, OrganizationNodeId, now));
        return Result.Success();
    }

    /// <summary>CNT-1/CNT-5: `Published -&gt; Archived`, manual or `expire_at`-driven (same method either way - requirement-spec.md §4: "never a live side effect," the CALLER decides when to invoke this, never a read path).</summary>
    public Result Archive(DateTimeOffset now)
    {
        if (Status != SchedulableStatus.Published)
        {
            return Result.Failure(Error.Conflict("notice.not_published", $"Notice '{Id}' cannot be archived - it is currently '{Status}'."));
        }

        Status = SchedulableStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
        Raise(new NoticeArchived(Id.Value, now));
        return Result.Success();
    }
}
