using System.Text.RegularExpressions;
using UMS.Shared.Audit;
using UMS.Shared.ErrorHandling.Results;

namespace UMS.Modules.Audit.Domain.Entries;

/// <summary>
/// A validated action verb (requirement-spec.md audit §2: "a fixed vocabulary:
/// create/update/delete/publish/approve/reject/assign/revoke, extensible per module but never
/// free text"). "Extensible" is satisfied by accepting any lowercase, underscore-separated token
/// beyond <see cref="AuditActions"/>' eight base constants; "never free text" is enforced by
/// rejecting anything containing whitespace, punctuation, or mixed case - a module-specific verb
/// (e.g. Finance's <c>"refund"</c>) is still a single controlled token, never a sentence.
/// </summary>
public sealed partial record AuditAction
{
    private const int MaxLength = 64;

    /// <summary>
    /// Actions that are inherently reversal-shaped regardless of which module raises them - a
    /// reject or a revoke always overturns a prior decision by definition. Every other
    /// reversal-shaped action (a grade correction, a re-publication) is a plain
    /// <see cref="AuditActions.Update"/>/<see cref="AuditActions.Publish"/> whose reversal nature
    /// is only knowable to the calling module - see <see cref="RecordAuditEntryRequest.IsCorrection"/>,
    /// which is how those cases still reach the mandatory-reason check (requirement-spec.md audit
    /// §4: "Reason is mandatory for reversal-shaped actions").
    /// </summary>
    private static readonly HashSet<string> InherentlyReversalActions = [AuditActions.Reject, AuditActions.Revoke];

    private AuditAction(string value)
    {
        Value = value;
    }

    public string Value { get; }

    public bool IsInherentlyReversal => InherentlyReversalActions.Contains(Value);

    public static Result<AuditAction> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Error.Validation("audit_action.required", "Action is required.");
        }

        var normalized = value.Trim();

        if (normalized.Length > MaxLength)
        {
            return Error.Validation("audit_action.too_long", $"Action must be at most {MaxLength} characters.");
        }

        return ActionTokenPattern().IsMatch(normalized)
            ? new AuditAction(normalized)
            : Error.Validation(
                "audit_action.invalid_format",
                "Action must be a single lowercase, snake_case token (e.g. 'update', 'refund') - never free text.");
    }

    public override string ToString() => Value;

    [GeneratedRegex("^[a-z][a-z0-9_]*$", RegexOptions.Compiled)]
    private static partial Regex ActionTokenPattern();
}
