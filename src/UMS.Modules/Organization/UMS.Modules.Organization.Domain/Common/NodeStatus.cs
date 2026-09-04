namespace UMS.Modules.Organization.Domain.Common;

/// <summary>
/// The only lifecycle a hierarchy node above `Room`/`Building` ever has (design-decisions.md,
/// "Soft-Delete/Deactivate-Only Pattern": "a status flag (Active/Inactive) is the only lifecycle
/// transition for University/Campus/Faculty/Department/Program"). There is deliberately no
/// `Deleted` member - hard delete is not a concept these five levels have at all.
/// </summary>
public enum NodeStatus
{
    Active = 0,
    Inactive = 1,
}
