using UMS.Modules.Content.Domain.Common;
using UMS.Modules.Content.Domain.Events;

namespace UMS.Modules.Content.UnitTests.Events;

/// <summary>CNT-7: requirement-spec.md §2.2/§9 - no publish_at/expire_at state machine; the bilingual-completeness gate applies at Create/UpdateContent directly instead of at a publish transition Event doesn't have.</summary>
public sealed class EventTests
{
    private static readonly DateTimeOffset Start = new(2026, 6, 1, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset End = Start.AddHours(2);
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void Create_of_Public_event_with_only_English_is_rejected()
    {
        var result = Event.Create("Title", "Body", null, ContentAudience.Public, null, Start, End, Actor, Start);

        Assert.True(result.IsFailure);
        Assert.Equal("event.bilingual_incomplete", result.Error!.Code);
    }

    [Fact]
    public void Create_of_Public_event_with_an_inline_Bengali_translation_succeeds()
    {
        // Regression test: Event has no Draft state to stage a translation in before a publish
        // transition (it has none) - Create's optional inline-translation parameters are the ONLY
        // way a Public Event can ever be created at all, since no translation row can exist before
        // the aggregate itself does.
        var result = Event.Create("Title", "Body", null, ContentAudience.Public, null, Start, End, Actor, Start, "bn", "শিরোনাম", "বিষয়বস্তু");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Translations);
    }

    [Fact]
    public void Create_of_Admin_only_event_with_only_English_is_exempt_and_succeeds()
    {
        var result = Event.Create("Title", "Body", null, ContentAudience.Admin, null, Start, End, Actor, Start);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Create_rejects_EndAt_not_strictly_after_StartAt()
    {
        var result = Event.Create("Title", "Body", null, ContentAudience.Admin, null, Start, Start, Actor, Start);

        Assert.True(result.IsFailure);
        Assert.Equal("event.invalid_window", result.Error!.Code);
    }

    [Fact]
    public void UpdateContent_of_Public_event_re_validates_the_gate_and_succeeds_once_a_translation_exists()
    {
        var publicEvent = Event.Create("Title", "Body", null, ContentAudience.Public, null, Start, End, Actor, Start, "bn", "শিরোনাম", "বিষয়বস্তু").Value;

        var result = publicEvent.UpdateContent("Updated Title", "Body", null, Start, End, Actor, Start);

        Assert.True(result.IsSuccess);
        Assert.Equal("Updated Title", publicEvent.Title);
    }

    [Fact]
    public void IsUpcoming_is_true_while_now_is_before_EndAt()
    {
        var calendarEvent = Event.Create("Title", "Body", null, ContentAudience.Admin, null, Start, End, Actor, Start).Value;

        Assert.True(calendarEvent.IsUpcoming(Start));
        Assert.False(calendarEvent.IsUpcoming(End.AddMinutes(1)));
    }
}
