using UMS.Modules.Audit.Application.Entries;
using UMS.Modules.Audit.Application.Exports;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.UnitTests.Exports;

public class AuditEntryCsvRendererTests
{
    private static AuditLogEntryDto CreateDto(string reason = "") => new(
        "01ABCDEFGHJKMNPQRSTVWXYZ0",
        DateTimeOffset.Parse("2026-09-04T10:00:00Z", System.Globalization.CultureInfo.InvariantCulture),
        "usr_123",
        AuditActorType.User,
        "203.0.113.7",
        "ums-admin-web",
        "Grade",
        "grade_456",
        AuditActions.Update,
        """{"score":68}""",
        """{"score":74}""",
        "corr_789",
        reason,
        null);

    [Fact]
    public void Render_writes_a_header_row_and_one_row_per_entry()
    {
        var csv = AuditEntryCsvRenderer.Render([CreateDto(), CreateDto()]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 rows
        Assert.StartsWith("id,occurredAt,actorId", lines[0]);
    }

    [Fact]
    public void Render_quotes_a_field_containing_a_comma()
    {
        var csv = AuditEntryCsvRenderer.Render([CreateDto(reason: "Reversed, per policy")]);

        Assert.Contains("\"Reversed, per policy\"", csv);
    }

    [Fact]
    public void Render_escapes_embedded_double_quotes()
    {
        var csv = AuditEntryCsvRenderer.Render([CreateDto(reason: "Said \"urgent\" review")]);

        Assert.Contains("\"Said \"\"urgent\"\" review\"", csv);
    }

    [Fact]
    public void Render_produces_only_the_header_for_an_empty_result_set()
    {
        var csv = AuditEntryCsvRenderer.Render([]);

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
    }
}
