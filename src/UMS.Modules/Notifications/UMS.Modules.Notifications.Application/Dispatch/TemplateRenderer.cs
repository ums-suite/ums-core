using System.Text.Json;
using System.Text.RegularExpressions;

namespace UMS.Modules.Notifications.Application.Dispatch;

/// <summary>
/// requirement-spec.md §2 Template Management - renders a resolved <c>TemplateTranslation</c>'s
/// <c>{{mergeField}}</c> placeholders against a <see cref="NotificationRequest.PayloadJson"/>
/// merge-field object. Deliberately minimal (no conditionals/loops) - the spec never asks for a full
/// templating language, only "bilingual ... channel-specific" text with simple substitution.
/// </summary>
public static partial class TemplateRenderer
{
    public static string Render(string template, string payloadJson)
    {
        Dictionary<string, string>? mergeFields;
        try
        {
            mergeFields = JsonSerializer.Deserialize<Dictionary<string, string>>(payloadJson);
        }
        catch (JsonException)
        {
            mergeFields = null;
        }

        mergeFields ??= [];

        return PlaceholderPattern().Replace(template, match =>
        {
            var key = match.Groups[1].Value;
            return mergeFields.TryGetValue(key, out var value) ? value : match.Value;
        });
    }

    [GeneratedRegex(@"\{\{\s*(\w+)\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}
