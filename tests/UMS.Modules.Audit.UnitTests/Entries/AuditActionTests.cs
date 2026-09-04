using UMS.Modules.Audit.Domain.Entries;
using UMS.Shared.Audit;

namespace UMS.Modules.Audit.UnitTests.Entries;

public class AuditActionTests
{
    [Theory]
    [InlineData(AuditActions.Create)]
    [InlineData(AuditActions.Update)]
    [InlineData(AuditActions.Delete)]
    [InlineData(AuditActions.Publish)]
    [InlineData(AuditActions.Approve)]
    [InlineData(AuditActions.Reject)]
    [InlineData(AuditActions.Assign)]
    [InlineData(AuditActions.Revoke)]
    public void Create_accepts_every_base_vocabulary_action(string action)
    {
        var result = AuditAction.Create(action);

        Assert.True(result.IsSuccess);
        Assert.Equal(action, result.Value.Value);
    }

    [Fact]
    public void Create_accepts_a_module_specific_extended_verb()
    {
        var result = AuditAction.Create("refund");

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_action(string? action)
    {
        var result = AuditAction.Create(action);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_action.required", result.Error!.Code);
    }

    [Theory]
    [InlineData("Update")]
    [InlineData("UPDATE")]
    [InlineData("update grade")]
    [InlineData("update-grade")]
    [InlineData("Approved the request because it looked fine")]
    public void Create_rejects_anything_that_is_not_a_single_lowercase_snake_case_token(string action)
    {
        var result = AuditAction.Create(action);

        Assert.True(result.IsFailure);
        Assert.Equal("audit_action.invalid_format", result.Error!.Code);
    }

    [Theory]
    [InlineData(AuditActions.Reject, true)]
    [InlineData(AuditActions.Revoke, true)]
    [InlineData(AuditActions.Update, false)]
    [InlineData(AuditActions.Create, false)]
    [InlineData("refund", false)]
    public void IsInherentlyReversal_is_true_only_for_reject_and_revoke(string action, bool expected)
    {
        var result = AuditAction.Create(action);

        Assert.Equal(expected, result.Value.IsInherentlyReversal);
    }
}
