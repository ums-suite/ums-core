using UMS.Modules.Audit.Domain.Entries;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.UnitTests.Entries;

public class AuditLogEntryTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-04T10:15:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static UMS.Shared.ErrorHandling.Results.Result<AuditLogEntry> RecordDefault(
        string actorId = "usr_123",
        AuditActorType actorType = AuditActorType.User,
        string? ipAddress = "203.0.113.7",
        string application = "ums-admin-web",
        string entityType = "Grade",
        string entityId = "grade_456",
        string action = AuditActions.Update,
        string? beforeValueJson = """{"score":68}""",
        string? afterValueJson = """{"score":74}""",
        string correlationId = "corr_789",
        string? reason = null,
        bool isCorrection = false,
        Guid? organizationScopeId = null) =>
        AuditLogEntry.Record(actorId, actorType, ipAddress, application, entityType, entityId, action, beforeValueJson, afterValueJson, correlationId, reason, isCorrection, organizationScopeId, Now);

    [Fact]
    public void Record_succeeds_for_a_well_formed_entry()
    {
        var result = RecordDefault();

        Assert.True(result.IsSuccess);
        var entry = result.Value;
        Assert.Equal(26, entry.Id.Value.Length);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("usr_123", entry.ActorId);
        Assert.Equal("Grade", entry.EntityType);
        Assert.Equal(AuditActions.Update, entry.Action.Value);
    }

    [Fact]
    public void Record_accepts_a_well_known_system_principal_as_actor()
    {
        var result = RecordDefault(actorId: "system:result-publisher", actorType: AuditActorType.System, ipAddress: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(AuditActorType.System, result.Value.ActorType);
        Assert.Null(result.Value.IpAddress);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Record_fails_when_actor_id_is_missing(string? actorId)
    {
        var result = RecordDefault(actorId: actorId!);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.actor_required", result.Error!.Code);
    }

    [Fact]
    public void Record_fails_when_correlation_id_is_missing()
    {
        var result = RecordDefault(correlationId: "");

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.correlation_id_required", result.Error!.Code);
    }

    [Theory]
    [InlineData("grade")]
    [InlineData("gradeChange")]
    public void Record_fails_when_entity_type_is_not_pascal_case(string entityType)
    {
        var result = RecordDefault(entityType: entityType);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.entity_type_invalid", result.Error!.Code);
    }

    [Fact]
    public void Record_fails_when_before_value_is_not_well_formed_json()
    {
        var result = RecordDefault(beforeValueJson: "{not json");

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.before_value_invalid", result.Error!.Code);
    }

    [Fact]
    public void Record_fails_when_after_value_is_not_well_formed_json()
    {
        var result = RecordDefault(afterValueJson: "{not json");

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.after_value_invalid", result.Error!.Code);
    }

    [Fact]
    public void Record_allows_null_before_and_after_values_for_pure_create_or_delete()
    {
        var created = RecordDefault(action: AuditActions.Create, beforeValueJson: null, afterValueJson: """{"score":90}""");
        var deleted = RecordDefault(action: AuditActions.Delete, beforeValueJson: """{"score":90}""", afterValueJson: null);

        Assert.True(created.IsSuccess);
        Assert.True(deleted.IsSuccess);
    }

    [Theory]
    [InlineData(AuditActions.Reject)]
    [InlineData(AuditActions.Revoke)]
    public void Record_fails_when_an_inherently_reversal_action_has_no_reason(string action)
    {
        var result = RecordDefault(action: action, reason: null);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.reason_required", result.Error!.Code);
    }

    [Theory]
    [InlineData(AuditActions.Reject)]
    [InlineData(AuditActions.Revoke)]
    public void Record_succeeds_when_an_inherently_reversal_action_has_a_reason(string action)
    {
        var result = RecordDefault(action: action, reason: "Department Head reversed the decision.");

        Assert.True(result.IsSuccess);
        Assert.Equal("Department Head reversed the decision.", result.Value.Reason);
    }

    [Fact]
    public void Record_does_not_require_a_reason_for_a_plain_update_by_default()
    {
        var result = RecordDefault(action: AuditActions.Update, reason: null, isCorrection: false);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.Reason);
    }

    [Fact]
    public void Record_requires_a_reason_when_the_caller_flags_the_mutation_as_a_correction()
    {
        var result = RecordDefault(action: AuditActions.Update, reason: null, isCorrection: true);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_entry.reason_required", result.Error!.Code);
    }

    [Fact]
    public void Record_succeeds_when_a_flagged_correction_has_a_reason()
    {
        var result = RecordDefault(
            action: AuditActions.Update,
            reason: "Original entry had a transcription error.",
            isCorrection: true);

        Assert.True(result.IsSuccess);
    }
}
