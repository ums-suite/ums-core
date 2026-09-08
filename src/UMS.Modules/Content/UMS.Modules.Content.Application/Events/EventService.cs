using UMS.Modules.Content.Application.Abstractions;
using UMS.Modules.Content.Application.Permissions;
using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;
using UMS.Shared.ErrorHandling.Results;
using UMS.Shared.Identity;

namespace UMS.Modules.Content.Application.Events;

/// <summary>CNT-7: calendar CRUD - requirement-spec.md §2.2/§9 (no publish_at/expire_at state machine; own start/end date is its visibility window).</summary>
public sealed class EventService(IEventRepository events, IUnitOfWork unitOfWork, IScopeGrantDirectory scopeGrants, IClock clock)
{
    public static EventDto ToDto(Event calendarEvent, string? preferredLanguage)
    {
        var wantsBengali = string.Equals(preferredLanguage, BilingualCompletenessGate.RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase);
        var translation = wantsBengali
            ? calendarEvent.Translations.FirstOrDefault(t => string.Equals(t.LanguageCode, BilingualCompletenessGate.RequiredSecondLanguage, StringComparison.OrdinalIgnoreCase))
            : null;

        var (title, body, locationLabel, languageCode) = translation is not null
            ? (translation.Title, translation.Body, translation.LocationLabel, translation.LanguageCode)
            : (calendarEvent.Title, calendarEvent.Body, calendarEvent.LocationLabel, "en");

        return new EventDto(
            calendarEvent.Id.Value,
            title,
            body,
            locationLabel,
            languageCode,
            Enum.GetValues<ContentAudience>().Where(v => v != ContentAudience.None && calendarEvent.Audience.HasFlag(v)).Select(v => v.ToString()).ToArray(),
            calendarEvent.OrganizationNodeId,
            calendarEvent.StartAt,
            calendarEvent.EndAt,
            calendarEvent.CreatedAt,
            calendarEvent.UpdatedAt,
            calendarEvent.Version);
    }

    /// <param name="translationLanguageCode">
    /// Optional inline non-English (`"bn"`) translation, supplied at creation time - see
    /// <see cref="Event.Create"/>'s own remarks on why this is the only way a Public-audience Event
    /// can ever be created bilingual-complete (it has no Draft state to add a translation into
    /// afterward before some later publish transition, since it has none).
    /// </param>
    public async Task<Result<EventDto>> CreateAsync(string title, string body, string? locationLabel, ContentAudience audience, Guid? organizationNodeId, DateTimeOffset startAt, DateTimeOffset endAt, Guid actorUserId, string? translationLanguageCode = null, string? translationTitle = null, string? translationBody = null, string? translationLocationLabel = null, CancellationToken cancellationToken = default)
    {
        var created = Event.Create(title, body, locationLabel, audience, organizationNodeId, startAt, endAt, actorUserId, clock.UtcNow, translationLanguageCode, translationTitle, translationBody, translationLocationLabel);
        if (created.IsFailure)
        {
            return created.Error!;
        }

        events.Add(created.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ToDto(created.Value, preferredLanguage: null);
    }

    public async Task<Result<EventDto>> UpdateContentAsync(Guid id, string title, string body, string? locationLabel, DateTimeOffset startAt, DateTimeOffset endAt, Guid actorUserId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await events.GetByIdAsync(new EventId(id), cancellationToken).ConfigureAwait(false);
        if (calendarEvent is null)
        {
            return Error.NotFound("event.not_found", $"No Event exists with id '{id}'.");
        }

        var updated = calendarEvent.UpdateContent(title, body, locationLabel, startAt, endAt, actorUserId, clock.UtcNow);
        if (updated.IsFailure)
        {
            return updated.Error!;
        }

        unitOfWork.SetExpectedVersion(calendarEvent, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("event.concurrency_conflict", ex.Message);
        }

        return ToDto(calendarEvent, preferredLanguage: null);
    }

    public async Task<Result<EventDto>> UpsertTranslationAsync(Guid id, string languageCode, string title, string body, string? locationLabel, Guid actorUserId, uint expectedVersion, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await events.GetByIdAsync(new EventId(id), cancellationToken).ConfigureAwait(false);
        if (calendarEvent is null)
        {
            return Error.NotFound("event.not_found", $"No Event exists with id '{id}'.");
        }

        var upsertResult = calendarEvent.UpsertTranslation(languageCode, title, body, locationLabel, actorUserId, clock.UtcNow);
        if (upsertResult.IsFailure)
        {
            return upsertResult.Error!;
        }

        unitOfWork.SetExpectedVersion(calendarEvent, expectedVersion);

        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (ConcurrencyConflictException ex)
        {
            return Error.Conflict("event.concurrency_conflict", ex.Message);
        }

        return ToDto(calendarEvent, preferredLanguage: null);
    }

    public async Task<Result<EventDto>> GetByIdAsync(Guid id, string? preferredLanguage, Guid? callerUserId, CancellationToken cancellationToken = default)
    {
        var calendarEvent = await events.GetByIdAsync(new EventId(id), cancellationToken).ConfigureAwait(false);
        if (calendarEvent is null)
        {
            return Error.NotFound("event.not_found", $"No Event exists with id '{id}'.");
        }

        if (!await IsVisibleToCallerAsync(calendarEvent, callerUserId, cancellationToken).ConfigureAwait(false))
        {
            return Error.NotFound("event.not_found", $"No Event exists with id '{id}'.");
        }

        return ToDto(calendarEvent, preferredLanguage);
    }

    /// <summary>CNT-3, reused identically for Event per requirement-spec.md §2.2 "the same audience-scoping model as Notice."</summary>
    public async Task<bool> IsVisibleToCallerAsync(Event calendarEvent, Guid? callerUserId, CancellationToken cancellationToken)
    {
        if (calendarEvent.Audience.HasFlag(ContentAudience.Public))
        {
            return true;
        }

        if (callerUserId is null)
        {
            return false;
        }

        if (calendarEvent.OrganizationNodeId is null)
        {
            return true;
        }

        return await scopeGrants.HasPermissionAtScopeAsync(callerUserId.Value, ContentPermissions.NoticeRead, calendarEvent.OrganizationNodeId.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>CNT-7: `GET /content/events` - date-range/audience/Organization-node filtering (requirement-spec.md §2.2).</summary>
    public async Task<EventListPage> ListAsync(DateTimeOffset? from, DateTimeOffset? to, ContentAudience? audience, Guid? organizationNodeId, Guid? callerUserId, string? preferredLanguage, int skip, int take, CancellationToken cancellationToken = default)
    {
        skip = Math.Max(skip, 0);
        take = Math.Clamp(take, 1, 200);

        var requestedAudience = audience ?? ContentAudience.Public;
        var candidates = await events.ListAsync(from, to, requestedAudience, organizationNodeId, skip, take, cancellationToken).ConfigureAwait(false);

        var visible = new List<EventDto>();
        foreach (var calendarEvent in candidates)
        {
            if (await IsVisibleToCallerAsync(calendarEvent, callerUserId, cancellationToken).ConfigureAwait(false))
            {
                visible.Add(ToDto(calendarEvent, preferredLanguage));
            }
        }

        var total = await events.CountAsync(from, to, requestedAudience, organizationNodeId, cancellationToken).ConfigureAwait(false);
        return new EventListPage(visible, total, skip, take);
    }
}
