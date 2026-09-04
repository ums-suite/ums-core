using System.Globalization;
using System.Text;
using UMS.Modules.Audit.Application.Entries;

namespace UMS.Modules.Audit.Application.Exports;

/// <summary>AUD-9: renders a filtered set of audit entries as CSV - pure, DB/storage-free so it is unit testable on its own.</summary>
public static class AuditEntryCsvRenderer
{
    private static readonly string[] Header =
    [
        "id", "occurredAt", "actorId", "actorType", "ipAddress", "application",
        "entityType", "entityId", "action", "beforeValue", "afterValue", "correlationId", "reason",
    ];

    public static string Render(IEnumerable<AuditLogEntryDto> entries)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', Header));

        foreach (var entry in entries)
        {
            var fields = new[]
            {
                entry.Id,
                entry.OccurredAt.ToString("O", CultureInfo.InvariantCulture),
                entry.ActorId,
                entry.ActorType.ToString(),
                entry.IpAddress ?? string.Empty,
                entry.Application,
                entry.EntityType,
                entry.EntityId,
                entry.Action,
                entry.BeforeValue ?? string.Empty,
                entry.AfterValue ?? string.Empty,
                entry.CorrelationId,
                entry.Reason ?? string.Empty,
            };

            builder.AppendLine(string.Join(',', fields.Select(Escape)));
        }

        return builder.ToString();
    }

    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
        {
            return $"\"{field.Replace("\"", "\"\"")}\"";
        }

        return field;
    }
}
