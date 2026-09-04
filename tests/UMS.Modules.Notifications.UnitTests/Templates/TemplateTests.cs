using UMS.Modules.Notifications.Domain.Common;
using UMS.Modules.Notifications.Domain.Templates;

namespace UMS.Modules.Notifications.UnitTests.Templates;

/// <summary>design-decisions.md "Template Resolution Fallback - Server-Side English Fallback, Never a Blank Send".</summary>
public class TemplateTests
{
    private static readonly DateTimeOffset _now = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Resolve_returns_the_requested_language_when_present()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Email, _now).Value;
        template.UpsertTranslation("en", "Payment received", "Thanks!", null, null, _now);
        template.UpsertTranslation("bn", "পেমেন্ট গৃহীত", "ধন্যবাদ!", null, null, _now);

        var resolved = template.Resolve("bn");

        Assert.NotNull(resolved);
        Assert.Equal("bn", resolved!.LanguageCode);
    }

    [Fact]
    public void Resolve_falls_back_to_English_when_the_requested_language_is_missing()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Email, _now).Value;
        template.UpsertTranslation("en", "Payment received", "Thanks!", null, null, _now);

        var resolved = template.Resolve("bn");

        Assert.NotNull(resolved);
        Assert.Equal("en", resolved!.LanguageCode);
    }

    [Fact]
    public void Resolve_returns_null_when_English_is_also_missing_never_a_blank_send()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Email, _now).Value;
        template.UpsertTranslation("bn", "পেমেন্ট গৃহীত", "ধন্যবাদ!", null, null, _now);

        var resolved = template.Resolve("fr");

        Assert.Null(resolved);
    }

    [Fact]
    public void UpsertTranslation_requires_a_subject_for_the_Email_channel()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Email, _now).Value;

        var result = template.UpsertTranslation("en", subject: null, body: "Thanks!", pushTitle: null, deepLink: null, _now);

        Assert.True(result.IsFailure);
        Assert.Equal("template_translation.subject_required", result.Error!.Code);
    }

    [Fact]
    public void UpsertTranslation_requires_a_push_title_for_the_Push_channel()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Push, _now).Value;

        var result = template.UpsertTranslation("en", subject: null, body: "Thanks!", pushTitle: null, deepLink: null, _now);

        Assert.True(result.IsFailure);
        Assert.Equal("template_translation.push_title_required", result.Error!.Code);
    }

    [Fact]
    public void UpsertTranslation_rejects_an_overlong_SMS_body()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Sms, _now).Value;

        var result = template.UpsertTranslation("en", subject: null, body: new string('x', 481), pushTitle: null, deepLink: null, _now);

        Assert.True(result.IsFailure);
        Assert.Equal("template_translation.body_too_long", result.Error!.Code);
    }

    [Fact]
    public void UpsertTranslation_replaces_an_existing_translation_for_the_same_language_and_bumps_version()
    {
        var template = Template.Create("PaymentCompleted", NotificationChannel.Email, _now).Value;
        template.UpsertTranslation("en", "v1 subject", "v1 body", null, null, _now);
        var versionAfterFirst = template.Version;

        template.UpsertTranslation("en", "v2 subject", "v2 body", null, null, _now);

        Assert.Single(template.Translations);
        Assert.Equal("v2 subject", template.Translations.Single().Subject);
        Assert.True(template.Version > versionAfterFirst);
    }
}
