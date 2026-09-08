using UMS.Modules.Content.Domain.Common;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Content.Api;

internal static class AudienceParser
{
    public static Result<ContentAudience> Parse(string[]? values)
    {
        if (values is null || values.Length == 0)
        {
            return Error.Validation("audience.required", "At least one audience value is required (Public, Student, Faculty, Admin).");
        }

        var audience = ContentAudience.None;
        foreach (var value in values)
        {
            if (!Enum.TryParse<ContentAudience>(value, ignoreCase: true, out var parsed) || parsed == ContentAudience.None)
            {
                return Error.Validation("audience.invalid", $"'{value}' is not a recognized audience (expected Public, Student, Faculty, or Admin).");
            }

            audience |= parsed;
        }

        return audience;
    }

    public static ContentAudience? ParseSingle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Enum.TryParse<ContentAudience>(value, ignoreCase: true, out var parsed) && parsed != ContentAudience.None ? parsed : null;
    }
}
