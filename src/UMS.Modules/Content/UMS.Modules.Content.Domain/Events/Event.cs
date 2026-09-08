using UMS.Modules.Content.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Domain.Events;

/// <summary>
/// CNT-7: glossary "a university-wide or audience-scoped calendar event." requirement-spec.md
/// §2.2/§9: deliberately NO `publish_at`/`expire_at` state machine - its own
/// <see cref="StartAt"/>/<see cref="EndAt"/> window IS its visibility window
/// (<see cref="IsUpcoming"/>), no separate archive step.
///
/// <para>
/// The bilingual-completeness gate (design-decisions.md) applies at <see cref="Create"/> and
/// <see cref="UpdateContent"/> directly, rather than at a publish transition Event doesn't have -
/// the same shared <see cref="BilingualCompletenessGate"/> function Notice's three call sites use.
/// </para>
/// </summary>
public sealed class Event : AggregateRoot<EventId>
{
    private readonly List<EventTranslation> _translations = [];

    private Event()
    {
    }

    private Event(EventId id, string title, string body, string? locationLabel, ContentAudience audience, Guid? organizationNodeId, DateTimeOffset startAt, DateTimeOffset endAt, Guid createdByUserId, DateTimeOffset now)
    {
        Id = id;
        Title = title;
        Body = body;
        LocationLabel = locationLabel;
        Audience = audience;
        OrganizationNodeId = organizationNodeId;
        StartAt = startAt;
        EndAt = endAt;
        CreatedByUserId = createdByUserId;
        UpdatedByUserId = createdByUserId;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public string Title { get; private set; } = string.Empty;

    public string Body { get; private set; } = string.Empty;

    public string? LocationLabel { get; private set; }

    public ContentAudience Audience { get; private set; }

    public Guid? OrganizationNodeId { get; private set; }

    public DateTimeOffset StartAt { get; private set; }

    public DateTimeOffset EndAt { get; private set; }

    public Guid CreatedByUserId { get; private set; }

    public Guid UpdatedByUserId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public IReadOnlyCollection<EventTranslation> Translations => _translations.AsReadOnly();

    public static Result<Event> Create(string title, string body, string? locationLabel, ContentAudience audience, Guid? organizationNodeId, DateTimeOffset startAt, DateTimeOffset endAt, Guid createdByUserId, DateTimeOffset now)
    {
        var validation = Validate(title, body, audience, startAt, endAt, []);
        if (validation.IsFailure)
        {
            return validation.Error!;
        }

        return new Event(EventId.New(), title.Trim(), body.Trim(), string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim(), audience, organizationNodeId, startAt, endAt, createdByUserId, now);
    }

    public bool IsUpcoming(DateTimeOffset now) => EndAt >= now;

    public Result UpdateContent(string title, string body, string? locationLabel, DateTimeOffset startAt, DateTimeOffset endAt, Guid actorUserId, DateTimeOffset now)
    {
        var validation = Validate(title, body, Audience, startAt, endAt, _translations.Select(t => t.LanguageCode));
        if (validation.IsFailure)
        {
            return Result.Failure(validation.Error!);
        }

        Title = title.Trim();
        Body = body.Trim();
        LocationLabel = string.IsNullOrWhiteSpace(locationLabel) ? null : locationLabel.Trim();
        StartAt = startAt;
        EndAt = endAt;
        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        return Result.Success();
    }

    /// <summary>CNT-2: upserts the one `(languageCode)` translation row, mirroring <see cref="Notices.Notice.UpsertTranslation"/> exactly.</summary>
    public Result UpsertTranslation(string languageCode, string title, string body, string? locationLabel, Guid actorUserId, DateTimeOffset now)
    {
        if (string.Equals(languageCode?.Trim(), "en", StringComparison.OrdinalIgnoreCase))
        {
            return Result.Failure(Error.Validation("event.translation_english_not_allowed", "English is the Event's own canonical Title/Body - it is not stored as a translation row."));
        }

        var existing = _translations.FirstOrDefault(t => string.Equals(t.LanguageCode, languageCode!.Trim(), StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Update(title, body, locationLabel);
        }
        else
        {
            _translations.Add(EventTranslation.Create(languageCode!, title, body, locationLabel));
        }

        UpdatedByUserId = actorUserId;
        UpdatedAt = now;
        return Result.Success();
    }

    private static Result Validate(string title, string body, ContentAudience audience, DateTimeOffset startAt, DateTimeOffset endAt, IEnumerable<string> translatedLanguageCodes)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return Result.Failure(Error.Validation("event.title_required", "An Event requires a title."));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return Result.Failure(Error.Validation("event.body_required", "An Event requires body text."));
        }

        if (audience == ContentAudience.None)
        {
            return Result.Failure(Error.Validation("event.audience_required", "An Event must target at least one audience."));
        }

        if (endAt <= startAt)
        {
            return Result.Failure(Error.Validation("event.invalid_window", "An Event's end date/time must be strictly after its start date/time."));
        }

        if (!BilingualCompletenessGate.IsSatisfied(audience, title, body, translatedLanguageCodes))
        {
            return Result.Failure(Error.Validation("event.bilingual_incomplete", "A Public-audience Event requires both English and Bengali translations."));
        }

        return Result.Success();
    }
}
