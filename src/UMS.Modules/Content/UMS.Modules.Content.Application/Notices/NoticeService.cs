using System.Text.Json;
using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Common;
using UMS.Modules.Content.Application.Permissions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Notices;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;

namespace UMS.Modules.Content.Application.Notices;

/// <summary>
/// CNT-1/2/3/5/6/12/15: Notice CRUD, lifecycle, localization, audience scoping, and audit logging.
///
/// <para>
/// <b>Concurrency</b> (design-decisions.md): every write below that mutates an existing Notice
/// (<see cref="EditContentAsync"/>, <see cref="UpsertTranslationAsync"/>,
/// <see cref="UpdateScheduleAsync"/>, <see cref="ScheduleAsync"/>, <see cref="PublishAsync"/>,
/// <see cref="ArchiveAsync"/>) calls <see cref="IUnitOfWork.SetExpectedVersion{TEntity}"/> with the
/// caller-supplied version BEFORE saving - a stale write throws
/// <see cref="ConcurrencyConflictException"/>, translated to a 409 Conflict by
/// <see cref="TransactionalAuditWriter"/>. <see cref="Application.Scheduling.NoticeSchedulingService"/>
/// (the background job) calls the exact same <see cref="PublishAsync"/>/<see cref="ArchiveAsync"/>
/// methods with its own last-read version - it is not a special case.
/// </para>
/// </summary>
public sealed class NoticeService(
    INoticeRepository notices,
    IUnitOfWork unitOfWork,
    IAuditRecorder auditRecorder,
    IScopeGrantDirectory scopeGrants,
    ICacheInvalidator cacheInvalidator,
    IClock clock)
{
    public static NoticeDto ToDto(Notice notice, string? preferredLanguage)
    {
        var wantsBengali = string.Equals(preferredLanguage, BilingualCompletenessGate.RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase);
        var translation = wantsBengali
            ? notice.Translations.FirstOrDefault(t => string.Equals(t.LanguageCode, BilingualCompletenessGate.RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase))
            : null;

        var (title, body, languageCode) = translation is not null
            ? (translation.Title, translation.Body, translation.LanguageCode)
            : (notice.Title, notice.Body, "en");

        return new NoticeDto(
            notice.Id.Value,
            title,
            body,
            languageCode,
            SplitAudience(notice.Audience),
            notice.OrganizationNodeId,
            notice.IsUrgent,
            notice.Status.ToString(),
            notice.PublishAt,
            notice.ExpireAt,
            notice.PublishedAt,
            notice.ArchivedAt,
            notice.CreatedAt,
            notice.UpdatedAt,
            notice.Translations.Any(t => string.Equals(t.LanguageCode, BilingualCompletenessGate.RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase)),
            notice.Version);
    }

    public async Task<Result<NoticeDto>> CreateAsync(string title, string body, ContentAudience audience, Guid? organizationNodeId, bool isUrgent, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var created = Notice.Create(title, body, audience, organizationNodeId, isUrgent, actorUserId, clock.UtcNow);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        notices.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value, preferredLanguage: null);
    }

    public async Task<Result<NoticeDto>> UpsertTranslationAsync(Guid id, string languageCode, string title, string body, Guid actorUserId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var upserted = notice.UpsertTranslation(languageCode, title, body, actorUserId, clock.UtcNow);
        if (upserted.IsFailure)
        {
            return upserted.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("notice.concurrency_conflict", ex.Message);
        }

        return ToDto(notice, preferredLanguage: null);
    }

    /// <summary>requirement-spec.md §2.1: metadata correction after publication is allowed, but audit-logged (design-decisions.md "Audit-Write Synchronicity") - never silently.</summary>
    public async Task<Result<NoticeDto>> EditContentAsync(Guid id, string title, string body, AuditContext audit, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var wasPublished = notice.Status == SchedulableStatus.Published;
        var before = new { title = notice.Title, body = notice.Body };

        var edited = notice.EditContent(title, body, audit.ActorUserId, clock.UtcNow);
        if (edited.IsFailure)
        {
            return edited.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        if (!wasPublished)
        {
            try
            {
                await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (ConcurrencyConflictException ex)
            {
                return Error.Conflict("notice.concurrency_conflict", ex.Message);
            }

            return ToDto(notice, preferredLanguage: null);
        }

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var after = new { title = notice.Title, body = notice.Body };
        var auditRequest = audit.ToRequest("Notice", id.ToString(), AuditActions.Update, JsonSerializer.Serialize(before), JsonSerializer.Serialize(after));
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        return committed.IsFailure ? committed.Error! : ToDto(notice, preferredLanguage: null);
    }

    public async Task<Result<NoticeDto>> UpdateScheduleAsync(Guid id, DateTimeOffset? publishAt, DateTimeOffset? expireAt, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var updated = notice.UpdateSchedule(publishAt, expireAt, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("notice.concurrency_conflict", ex.Message);
        }

        return ToDto(notice, preferredLanguage: null);
    }

    /// <summary>CNT-2: `Draft -&gt; Scheduled` - first of the bilingual-completeness gate's three independent call sites (<see cref="Notice.Schedule"/>).</summary>
    public async Task<Result<NoticeDto>> ScheduleAsync(Guid id, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var scheduled = notice.Schedule(clock.UtcNow);
        if (scheduled.IsFailure)
        {
            return scheduled.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("notice.concurrency_conflict", ex.Message);
        }

        return ToDto(notice, preferredLanguage: null);
    }

    /// <summary>
    /// CNT-1/CNT-5: manual `POST /notices/{id}/publish` - bypasses the schedule. Second of the
    /// bilingual-completeness gate's three call sites (<see cref="Notice.Publish"/> - the SAME
    /// method the scheduled job's own `Scheduled -&gt; Published` commit calls, see
    /// <see cref="Application.Scheduling.NoticeSchedulingService.PublishDueAsync"/> for the third).
    /// Audited synchronously (CNT-6), then a best-effort cache-invalidation attempt
    /// (design-decisions.md "Cache-Correctness Backstop" - never load-bearing, logged-and-continued
    /// on failure by the <see cref="ICacheInvalidator"/> implementation itself).
    /// </summary>
    public async Task<Result<NoticeDto>> PublishAsync(Guid id, AuditContext audit, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var statusBefore = notice.Status;
        var published = notice.Publish(clock.UtcNow);
        if (published.IsFailure)
        {
            return published.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Notice", id.ToString(), AuditActions.Publish, $"{{\"status\":\"{statusBefore}\"}}", "{\"status\":\"Published\"}");
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        await cacheInvalidator.InvalidateAsync(CacheKeyFor(id), cancellationToken).ConfigureAwait(false);
        return ToDto(notice, preferredLanguage: null);
    }

    /// <summary>CNT-1/CNT-5/CNT-14: `Published -&gt; Archived`, manual or expire_at-driven. edge-cases.md "CDN/Redis cache for a just-Archived notice": the cache purge is attempted, but <see cref="GetByIdAsync"/>'s own Archived-check is the real correctness backstop regardless of whether it succeeds.</summary>
    public async Task<Result<NoticeDto>> ArchiveAsync(Guid id, AuditContext audit, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        var archived = notice.Archive(clock.UtcNow);
        if (archived.IsFailure)
        {
            return archived.Error!;
        }

        unitOfWork.SetExpectedVersion(notice, expectedVersion);

        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var auditRequest = audit.ToRequest("Notice", id.ToString(), AuditActions.Update, "{\"status\":\"Published\"}", "{\"status\":\"Archived\"}");
        var committed = await TransactionalAuditWriter.CommitWithAuditAsync(unitOfWork, auditRecorder, transaction, auditRequest, cancellationToken).ConfigureAwait(false);
        if (committed.IsFailure)
        {
            return committed.Error!;
        }

        await cacheInvalidator.InvalidateAsync(CacheKeyFor(id), cancellationToken).ConfigureAwait(false);
        return ToDto(notice, preferredLanguage: null);
    }

    /// <summary>
    /// requirement-spec.md §4/§8/design-decisions.md "Cache-Correctness Backstop": the origin
    /// correctness guarantee - an Archived Notice ALWAYS reports as gone here, independent of
    /// whatever the CDN/Redis cache is still serving. The endpoint translates the
    /// <c>"notice.archived"</c> code to a bare 410, not the normal ProblemDetails 404 shape.
    /// Also enforces audience scoping (CNT-3, requirement-spec.md §4 "enforced server-side on every
    /// read") - reuses <see cref="IScopeGrantDirectory"/> directly, never a parallel scoping model.
    /// </summary>
    public async Task<Result<NoticeDto>> GetByIdAsync(Guid id, string? preferredLanguage, Guid? callerUserId, CancellationToken cancellationToken = default)
    {
        var notice = await notices.GetByIdAsync(new NoticeId(id), cancellationToken).ConfigureAwait(false);
        if (notice is null)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        if (notice.Status == SchedulableStatus.Archived)
        {
            return Error.NotFound("notice.archived", $"Notice '{id}' is archived and no longer publicly available.");
        }

        if (notice.Status != SchedulableStatus.Published)
        {
            // Not yet public - only a caller with the write permission can see a Draft/Scheduled
            // Notice; the endpoint itself is expected to have already gated this via
            // content.notice.write for the admin preview path. A public/unauthenticated caller
            // reaching here at all means the endpoint routed it wrong - fail closed regardless.
            if (callerUserId is null)
            {
                return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
            }
        }

        var visible = await IsVisibleToCallerAsync(notice, callerUserId, cancellationToken).ConfigureAwait(false);
        if (!visible)
        {
            return Error.NotFound("notice.not_found", $"No Notice exists with id '{id}'.");
        }

        return ToDto(notice, preferredLanguage);
    }

    /// <summary>CNT-3: the one server-side audience/scope check every read (public, single-item, and feed) funnels through.</summary>
    public async Task<bool> IsVisibleToCallerAsync(Notice notice, Guid? callerUserId, CancellationToken cancellationToken)
    {
        if (notice.Audience.HasFlag(ContentAudience.Public))
        {
            return true;
        }

        if (callerUserId is null)
        {
            return false;
        }

        if (notice.OrganizationNodeId is null)
        {
            return true;
        }

        return await scopeGrants.HasPermissionAtScopeAsync(callerUserId.Value, ContentPermissions.NoticeRead, notice.OrganizationNodeId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>requirement-spec.md §2.1/§6: `GET /content/notices` public path - Public audience, Published only.</summary>
    public async Task<NoticeListPage> ListPublicAsync(string? preferredLanguage, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var items = await notices.ListAsync(ContentAudience.Public, SchedulableStatus.Published, skip, take, cancellationToken).ConfigureAwait(false);
        var total = await notices.CountAsync(ContentAudience.Public, SchedulableStatus.Published, cancellationToken).ConfigureAwait(false);
        return new NoticeListPage(items.Select(n => ToDto(n, preferredLanguage)).ToList(), total, skip, take);
    }

    /// <summary>CNT-12: `GET /content/notices?audience=student|faculty|admin` - the SAME Notice aggregate, filtered by audience/scope at the query layer (requirement-spec.md §2.7).</summary>
    public async Task<NoticeListPage> ListForAudienceAsync(ContentAudience requestedAudience, Guid callerUserId, string? preferredLanguage, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var candidates = await notices.ListAsync(requestedAudience, SchedulableStatus.Published, skip, take, cancellationToken).ConfigureAwait(false);
        var visible = new List<NoticeDto>();
        foreach (var notice in candidates)
        {
            if (await IsVisibleToCallerAsync(notice, callerUserId, cancellationToken).ConfigureAwait(false))
            {
                visible.Add(ToDto(notice, preferredLanguage));
            }
        }

        var total = await notices.CountAsync(requestedAudience, SchedulableStatus.Published, cancellationToken).ConfigureAwait(false);
        return new NoticeListPage(visible, total, skip, take);
    }

    private static string CacheKeyFor(Guid noticeId) => $"content:notice:{noticeId}";

    private static string[] SplitAudience(ContentAudience audience) =>
        Enum.GetValues<ContentAudience>().Where(v => v != ContentAudience.None && audience.HasFlag(v)).Select(v => v.ToString()).ToArray();
}
