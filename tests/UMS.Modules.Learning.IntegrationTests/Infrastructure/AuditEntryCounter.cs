using Npgsql;

namespace UMS.Modules.Learning.IntegrationTests.Infrastructure;

/// <summary>
/// Reads Audit's own <c>audit_log_entries</c> table directly, by raw SQL against the real test
/// Postgres container - the only way to verify Learning's synchronous cross-module Audit writes
/// actually landed, since Audit deliberately exposes no read path back to a calling module
/// (ADR-0012). Mirrors the same direct-inspection approach Student's and Academic's own suites use
/// for their own Audit assertions.
/// </summary>
internal static class AuditEntryCounter
{
    public static async Task<int> CountAsync(LearningApiFixture fixture, string entityType, string entityId, string? action = null)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();

        var sql = action is null
            ? "SELECT COUNT(*) FROM audit.audit_log_entries WHERE entity_type = @entityType AND entity_id = @entityId"
            : "SELECT COUNT(*) FROM audit.audit_log_entries WHERE entity_type = @entityType AND entity_id = @entityId AND action = @action";

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("entityType", entityType);
        command.Parameters.AddWithValue("entityId", entityId);
        if (action is not null)
        {
            command.Parameters.AddWithValue("action", action);
        }

        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    /// <summary>Counts rows in Learning's own outbox for one event type - what the "the fan-out event was actually published" assertions read.</summary>
    public static async Task<int> CountOutboxAsync(LearningApiFixture fixture, string eventType, string containsFragment)
    {
        await using var connection = new NpgsqlConnection(fixture.PostgresConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(
            "SELECT COUNT(*) FROM learning.outbox_messages WHERE event_type = @eventType AND payload_json LIKE @fragment",
            connection);
        command.Parameters.AddWithValue("eventType", eventType);
        command.Parameters.AddWithValue("fragment", $"%{containsFragment}%");

        return Convert.ToInt32(await command.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}
